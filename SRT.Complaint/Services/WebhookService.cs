using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public class WebhookService(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IMaskingService masking,
    IDataProtectionProvider dataProtectionProvider,
    IWebhookUrlValidator urlValidator,
    ILogger<WebhookService> logger) : IWebhookService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly IDataProtector _secretProtector = dataProtectionProvider.CreateProtector("SRT.Complaint.WebhookSecret.v1");

    public async Task QueueAsync(string eventType, object payload, int? partnerId, CancellationToken ct = default)
    {
        var eventId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;
        var payloadJson = JsonSerializer.Serialize(new
        {
            id = eventId,
            type = eventType,
            occurredAt,
            data = payload
        }, JsonOpts);

        db.WebhookOutboxMessages.Add(new WebhookOutboxMessage
        {
            EventId = eventId,
            EventType = eventType,
            PartnerId = partnerId,
            Payload = payloadJson,
            OccurredAt = occurredAt,
            NextAttemptAt = occurredAt
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task DispatchPendingOutboxAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var messages = await db.WebhookOutboxMessages
            .Where(x => x.ProcessedAt == null && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.OccurredAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            try
            {
                var candidates = await db.Webhooks
                    .Include(x => x.ApiKey)
                    .Where(x => x.IsActive && x.ApiKey.IsActive &&
                                (x.ApiKey.ExpiresAt == null || x.ApiKey.ExpiresAt > now))
                    .ToListAsync(ct);

                var matching = candidates
                    .Where(x => WebhookMatchesEvent(x, message.EventType))
                    .Where(x => message.PartnerId.HasValue
                        ? x.ApiKey.PartnerId == message.PartnerId || x.ApiKey.DataAccessMode == "All"
                        : x.ApiKey.DataAccessMode == "All")
                    .ToList();

                var existingWebhookIds = await db.WebhookDeliveryLogs
                    .Where(x => x.OutboxMessageId == message.Id)
                    .Select(x => x.WebhookId)
                    .ToListAsync(ct);

                foreach (var webhook in matching.Where(x => !existingWebhookIds.Contains(x.Id)))
                {
                    db.WebhookDeliveryLogs.Add(new WebhookDeliveryLog
                    {
                        WebhookId = webhook.Id,
                        OutboxMessageId = message.Id,
                        DeliveryId = Guid.NewGuid(),
                        EventType = message.EventType,
                        Payload = message.Payload,
                        AttemptCount = 0,
                        CreatedAt = now,
                        NextRetryAt = now
                    });
                }

                message.ProcessedAt = now;
                message.LastError = null;
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                message.AttemptCount++;
                message.LastError = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                message.NextAttemptAt = DateTime.UtcNow.AddMinutes(Math.Min(30, Math.Pow(2, message.AttemptCount)));
                await db.SaveChangesAsync(ct);
                logger.LogWarning(ex, "Failed to dispatch webhook outbox event {EventId}", message.EventId);
            }
        }

        await RetryPendingAsync(ct);
    }

    public async Task<IReadOnlyList<Webhook>> ListByApiKeyAsync(int apiKeyId, CancellationToken ct = default)
        => await db.Webhooks
            .Where(w => w.ApiKeyId == apiKeyId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Webhook>> ListAllAsync(CancellationToken ct = default)
        => await db.Webhooks
            .Include(w => w.ApiKey).ThenInclude(k => k.Partner)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

    public async Task<(Webhook webhook, string rawSecret)> CreateAsync(
        int apiKeyId, string name, string targetUrl, IList<string> events, CancellationToken ct = default)
    {
        var (isValid, error) = await urlValidator.ValidateAsync(targetUrl, ct);
        if (!isValid) throw new InvalidOperationException(error);

        var apiKeyExists = await db.ApiKeys.AnyAsync(x => x.Id == apiKeyId && x.IsActive, ct);
        if (!apiKeyExists) throw new InvalidOperationException("API key not found or inactive");

        var rawSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var webhook = new Webhook
        {
            ApiKeyId = apiKeyId,
            Name = name.Trim(),
            TargetUrl = targetUrl.Trim(),
            SecretHash = $"dp:v1:{_secretProtector.Protect(rawSecret)}",
            Events = JsonSerializer.Serialize(events.Distinct(StringComparer.OrdinalIgnoreCase)),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Webhooks.Add(webhook);
        await db.SaveChangesAsync(ct);
        return (webhook, rawSecret);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var webhook = await db.Webhooks.FindAsync([id], ct);
        if (webhook == null) return false;
        webhook.IsActive = false;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task RetryPendingAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var pending = await db.WebhookDeliveryLogs
            .Include(l => l.Webhook).ThenInclude(w => w.ApiKey)
            .Include(l => l.OutboxMessage)
            .Where(l => !l.IsDelivered && l.NextRetryAt != null && l.NextRetryAt <= now &&
                        l.AttemptCount < 5 && l.Webhook.IsActive && l.Webhook.ApiKey.IsActive)
            .OrderBy(l => l.NextRetryAt)
            .Take(100)
            .ToListAsync(ct);

        foreach (var log in pending)
        {
            log.AttemptCount++;
            await db.SaveChangesAsync(ct);
            await DeliverAsync(log.Webhook, log, ct);
        }
    }

    private async Task DeliverAsync(Webhook webhook, WebhookDeliveryLog log, CancellationToken ct)
    {
        try
        {
            var (isValid, error) = await urlValidator.ValidateAsync(webhook.TargetUrl, ct);
            if (!isValid) throw new InvalidOperationException(error);

            var rawSecret = UnprotectSecret(webhook.SecretHash);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var signature = ComputeSignature(timestamp, log.Payload, rawSecret);
            var client = httpClientFactory.CreateClient("Webhook");

            using var req = new HttpRequestMessage(HttpMethod.Post, webhook.TargetUrl)
            {
                Content = new StringContent(log.Payload, Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("X-SRT-Event", log.EventType);
            req.Headers.TryAddWithoutValidation("X-SRT-Event-Id", log.OutboxMessage?.EventId.ToString() ?? string.Empty);
            req.Headers.TryAddWithoutValidation("X-SRT-Delivery-Id", log.DeliveryId.ToString());
            req.Headers.TryAddWithoutValidation("X-SRT-Signature", $"v1={signature}");
            req.Headers.TryAddWithoutValidation("X-SRT-Timestamp", timestamp);

            using var response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            log.ResponseStatus = (int)response.StatusCode;
            log.ResponseBody = await ReadLimitedBodyAsync(response.Content, 2000, ct);
            log.IsDelivered = response.IsSuccessStatusCode;
            log.DeliveredAt = response.IsSuccessStatusCode ? DateTime.UtcNow : null;
            log.NextRetryAt = response.IsSuccessStatusCode ? null : NextRetryTime(log.AttemptCount);

            webhook.LastTriggeredAt = DateTime.UtcNow;
            webhook.LastStatusCode = (int)response.StatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Webhook delivery failed for webhook {WebhookId}", webhook.Id);
            log.ResponseBody = "Delivery failed";
            log.IsDelivered = false;
            log.NextRetryAt = NextRetryTime(log.AttemptCount);
        }
        finally
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private string UnprotectSecret(string protectedSecret)
    {
        if (protectedSecret.StartsWith("dp:v1:", StringComparison.Ordinal))
            return _secretProtector.Unprotect(protectedSecret[6..]);
        return masking.Decrypt(Convert.FromBase64String(protectedSecret));
    }

    private static string ComputeSignature(string timestamp, string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var canonicalBytes = Encoding.UTF8.GetBytes($"{timestamp}.{payload}");
        return Convert.ToHexString(HMACSHA256.HashData(keyBytes, canonicalBytes)).ToLowerInvariant();
    }

    private bool WebhookMatchesEvent(Webhook webhook, string eventType)
    {
        try
        {
            var events = JsonSerializer.Deserialize<List<string>>(webhook.Events) ?? [];
            return events.Contains(eventType, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Webhook {WebhookId} has malformed Events JSON", webhook.Id);
            return false;
        }
    }

    private static async Task<string?> ReadLimitedBodyAsync(HttpContent content, int maxChars, CancellationToken ct)
    {
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            bufferSize: 1024, leaveOpen: false);
        var buffer = new char[maxChars];
        var total = 0;
        while (total < maxChars)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(total, maxChars - total), ct);
            if (read == 0) break;
            total += read;
        }

        return total == 0 ? null : new string(buffer, 0, total);
    }

    private static DateTime? NextRetryTime(byte attempt) => attempt switch
    {
        1 => DateTime.UtcNow.AddMinutes(1),
        2 => DateTime.UtcNow.AddMinutes(5),
        3 => DateTime.UtcNow.AddMinutes(30),
        4 => DateTime.UtcNow.AddHours(2),
        _ => null
    };
}
