using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public class NotificationService(AppDbContext db, IConfiguration config, ILogger<NotificationService> logger) : INotificationService
{
    public async Task SendAsync(string eventKey, string? toPhone, string? toEmail, Dictionary<string, string> placeholders, CancellationToken ct = default)
    {
        var template = await db.NotificationTemplates.FirstOrDefaultAsync(t => t.EventKey == eventKey, ct);
        if (template is null) return;

        if (template.IsSmsEnabled && !string.IsNullOrWhiteSpace(toPhone) && !string.IsNullOrWhiteSpace(template.SmsBody))
        {
            var result = await SendSmsAsync(toPhone, ResolvePlaceholders(template.SmsBody, placeholders), ct);
            await SaveDeliveryLogAsync(eventKey, "SMS", MaskPhone(toPhone), result, ct);
        }

        if (template.IsEmailEnabled && !string.IsNullOrWhiteSpace(toEmail) && !string.IsNullOrWhiteSpace(template.EmailBody))
        {
            var result = await SendEmailAsync(toEmail, ResolvePlaceholders(template.EmailSubject ?? eventKey, placeholders), ResolvePlaceholders(template.EmailBody, placeholders), ct);
            await SaveDeliveryLogAsync(eventKey, "Email", MaskEmail(toEmail), result, ct);
        }
    }

    private static string ResolvePlaceholders(string template, Dictionary<string, string> values)
    {
        foreach (var (k, v) in values)
            template = template.Replace($"{{{k}}}", v);
        return template;
    }

    private async Task<DeliveryResult> SendSmsAsync(string phone, string body, CancellationToken ct)
    {
        try
        {
            var gatewayUrl = config["Notifications:SmsGatewayUrl"];
            var apiKey = config["Notifications:SmsApiKey"];
            if (string.IsNullOrEmpty(gatewayUrl)) return new("Skipped", "SMS gateway is not configured");

            using var http = new HttpClient();
            using var response = await http.PostAsJsonAsync(gatewayUrl, new { phone, message = body, apiKey }, ct);
            response.EnsureSuccessStatusCode();
            return new("Success", null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SMS send failed to {Phone}", phone);
            return new("Failed", ex.Message);
        }
    }

    private async Task<DeliveryResult> SendEmailAsync(string toAddress, string subject, string body, CancellationToken ct)
    {
        try
        {
            var host = config["Notifications:SmtpHost"];
            if (string.IsNullOrEmpty(host)) return new("Skipped", "SMTP is not configured");

            var port = config.GetValue<int>("Notifications:SmtpPort", 587);
            var user = config["Notifications:SmtpUser"] ?? "";
            var pass = config["Notifications:SmtpPassword"] ?? "";

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(user));
            message.To.Add(MailboxAddress.Parse(toAddress));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = body };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls, ct);
            await smtp.AuthenticateAsync(user, pass, ct);
            await smtp.SendAsync(message, ct);
            await smtp.DisconnectAsync(true, ct);
            return new("Success", null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Email send failed to {Email}", toAddress);
            return new("Failed", ex.Message);
        }
    }

    private async Task SaveDeliveryLogAsync(string eventKey, string channel, string recipientMasked, DeliveryResult result, CancellationToken ct)
    {
        db.NotificationDeliveryLogs.Add(new NotificationDeliveryLog
        {
            EventKey = eventKey,
            Channel = channel,
            RecipientMasked = recipientMasked,
            Status = result.Status,
            ErrorMessage = result.Error is null ? null : result.Error[..Math.Min(result.Error.Length, 1000)],
            AttemptedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    private static string MaskPhone(string value) => value.Length <= 4 ? "****" : $"***-***-{value[^4..]}";

    private static string MaskEmail(string value)
    {
        var at = value.IndexOf('@');
        return at <= 0 ? "***" : $"{value[0]}***{value[at..]}";
    }

    private sealed record DeliveryResult(string Status, string? Error);
}
