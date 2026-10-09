using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public class WebhookService(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IMaskingService masking,
    ILogger<WebhookService> logger) : IWebhookService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task TriggerAsync(string eventType, object payload, CancellationToken ct = default)
    {
        var all = await db.Webhooks.Where(w => w.IsActive).ToListAsync(ct);
        var matching = all.Where(w => WebhookMatchesEvent(w, eventType)).ToList();
        if (matching.Count == 0) return;

        var payloadJson = JsonSerializer.Serialize(payload, JsonOpts);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        foreach (var webhook in matching)
        {
            var log = new WebhookDeliveryLog
            {
                WebhookId = webhook.Id,
                EventType = eventType,
                Payload = payloadJson,
                AttemptCount = 1,
                CreatedAt = DateTime.UtcNow
            };
            db.WebhookDeliveryLogs.Add(log);
            await db.SaveChangesAsync(ct);
            await DeliverAsync(webhook, log, payloadJson, timestamp, ct);
        }
    }

    public async Task<IReadOnlyList<Webhook>> ListByApiKeyAsync(int apiKeyId, CancellationToken ct = default)
        => await db.Webhooks
            .Where(w => w.ApiKeyId == apiKeyId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Webhook>> ListAllAsync(CancellationToken ct = default)
        => await db.Webhooks
            .Include(w => w.ApiKey)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

    public async Task<(Webhook webhook, string rawSecret)> CreateAsync(
        int apiKeyId, string name, string targetUrl, IList<string> events, CancellationToken ct = default)
    {
        var rawSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var webhook = new Webhook
        {
            ApiKeyId = apiKeyId,
            Name = name,
            TargetUrl = targetUrl,
            SecretHash = Convert.ToBase64String(masking.Encrypt(rawSecret)),
            Events = JsonSerializer.Serialize(events),
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
        db.Webhooks.Remove(webhook);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task RetryPendingAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var pending = await db.WebhookDeliveryLogs
            .Include(l => l.Webhook)
            .Where(l => !l.IsDelivered && l.NextRetryAt != null && l.NextRetryAt <= now && l.AttemptCount < 4)
            .ToListAsync(ct);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        foreach (var log in pending)
        {
            log.AttemptCount++;
            await db.SaveChangesAsync(ct);
            await DeliverAsync(log.Webhook, log, log.Payload, timestamp, ct);
        }
    }

    private async Task DeliverAsync(Webhook webhook, WebhookDeliveryLog log, string payloadJson, string timestamp, CancellationToken ct)
    {
        try
        {
            var rawSecret = masking.Decrypt(Convert.FromBase64String(webhook.SecretHash));
            var signature = ComputeSignature(payloadJson, rawSecret);
            var client = httpClientFactory.CreateClient("Webhook");

            using var req = new HttpRequestMessage(HttpMethod.Post, webhook.TargetUrl)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("X-SRT-Event", log.EventType);
            req.Headers.TryAddWithoutValidation("X-SRT-Signature", $"sha256={signature}");
            req.Headers.TryAddWithoutValidation("X-SRT-Timestamp", timestamp);

            using var response = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            log.ResponseStatus = (int)response.StatusCode;
            log.IsDelivered = response.IsSuccessStatusCode;
            log.DeliveredAt = response.IsSuccessStatusCode ? DateTime.UtcNow : null;
            log.NextRetryAt = response.IsSuccessStatusCode ? null : NextRetryTime(log.AttemptCount);

            webhook.LastTriggeredAt = DateTime.UtcNow;
            webhook.LastStatusCode = (int)response.StatusCode;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Webhook delivery failed for {Url}", webhook.TargetUrl);
            log.IsDelivered = false;
            log.NextRetryAt = NextRetryTime(log.AttemptCount);
        }
        finally
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private static string ComputeSignature(string payload, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        return Convert.ToHexString(HMACSHA256.HashData(keyBytes, payloadBytes)).ToLowerInvariant();
    }

    private bool WebhookMatchesEvent(Webhook w, string eventType)
    {
        try
        {
            var events = JsonSerializer.Deserialize<List<string>>(w.Events) ?? [];
            return events.Contains(eventType, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Webhook {WebhookId} has malformed Events JSON: {Events}", w.Id, w.Events);
            return false;
        }
    }

    private static DateTime? NextRetryTime(byte attempt) => attempt switch
    {
        1 => DateTime.UtcNow.AddMinutes(5),
        2 => DateTime.UtcNow.AddMinutes(30),
        3 => DateTime.UtcNow.AddHours(2),
        _ => null
    };
}
