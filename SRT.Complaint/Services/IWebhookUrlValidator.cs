namespace SRT.Complaint.Services;

public interface IWebhookUrlValidator
{
    Task<(bool IsValid, string? Error)> ValidateAsync(string targetUrl, CancellationToken ct = default);
}
