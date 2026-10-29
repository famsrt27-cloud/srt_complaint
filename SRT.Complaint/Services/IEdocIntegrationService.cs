#nullable enable
using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public interface IEdocIntegrationService
{
    bool IsEnabled { get; }
    IReadOnlyList<EdocBasketOption> GetBaskets();
    string? GetDefaultBasketId(int categoryId);
    Task<EdocDocumentLink?> GetByComplaintIdAsync(int complaintId, CancellationToken ct = default);
    Task<EdocDocumentLink> QueueAsync(int complaintId, int createdById, string? basketId = null, CancellationToken ct = default);
    Task<EdocDocumentLink> SendNowAsync(int complaintId, CancellationToken ct = default);
    Task<int> ProcessPendingAsync(int maxItems = 5, CancellationToken ct = default);
    Task RecoverStaleAsync(CancellationToken ct = default);
    Task<EdocDocumentLink> ReconcileAsync(int complaintId, CancellationToken ct = default);
    Task<EdocDocumentLink> TrackAsync(int complaintId, CancellationToken ct = default);
    Task<int> TrackDueAsync(int maxItems = 20, CancellationToken ct = default);
    Task RetryAsync(int complaintId, int actorId, CancellationToken ct = default);
    Task<EdocDocumentLink> RetryAttachmentsAsync(int complaintId, int actorId, CancellationToken ct = default);
}
