#nullable enable
using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public interface IExternalSyncService
{
    IReadOnlyList<IExternalSystemAdapter> GetAvailableSystems();
    Task<ExternalSyncLog> SyncAsync(string systemKey, int? triggeredById, CancellationToken ct = default);
    Task<IReadOnlyList<ExternalSyncLog>> GetRecentLogsAsync(int count = 20, CancellationToken ct = default);

    /// <summary>Imports a single externally-pushed complaint (e.g. a webhook payload), skipping if already imported.</summary>
    Task<(bool Imported, string? ReferenceNumber)> ImportFromWebhookAsync(
        ExternalComplaintDto item, string systemKey, CancellationToken ct = default);

    /// <summary>Applies a status update to a previously-imported complaint. Returns false if no matching complaint was found.</summary>
    Task<bool> ApplyStatusUpdateAsync(
        string systemKey, string externalId, string? newStatus, string? note, CancellationToken ct = default);
}
