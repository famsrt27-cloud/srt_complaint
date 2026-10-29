using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public interface IWebhookService
{
    Task QueueAsync(string eventType, object payload, int? partnerId, CancellationToken ct = default);
    Task DispatchPendingOutboxAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Webhook>> ListByApiKeyAsync(int apiKeyId, CancellationToken ct = default);
    Task<IReadOnlyList<Webhook>> ListAllAsync(CancellationToken ct = default);
    Task<(Webhook webhook, string rawSecret)> CreateAsync(int apiKeyId, string name, string targetUrl, IList<string> events, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task RetryPendingAsync(CancellationToken ct = default);
}
