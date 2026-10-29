#nullable enable
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public class ExternalSyncService(
    IEnumerable<IExternalSystemAdapter> adapters,
    AppDbContext db,
    IComplaintService complaintService,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<ExternalSyncService> logger) : IExternalSyncService
{
    private static readonly HashSet<string> AllowedPhotoContentTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/gif" };
    private readonly IReadOnlyList<IExternalSystemAdapter> _adapters = adapters.ToList();

    public IReadOnlyList<IExternalSystemAdapter> GetAvailableSystems() => _adapters;

    public async Task<ExternalSyncLog> SyncAsync(string systemKey, int? triggeredById, CancellationToken ct = default)
    {
        var adapter = _adapters.FirstOrDefault(a => a.SystemKey == systemKey)
            ?? throw new InvalidOperationException($"ไม่พบระบบ '{systemKey}'");

        var log = new ExternalSyncLog
        {
            ExternalSystem = systemKey,
            StartedAt      = DateTime.UtcNow,
            SyncStatus     = "Running",
            TriggeredById  = triggeredById
        };
        db.ExternalSyncLogs.Add(log);
        await db.SaveChangesAsync(ct);

        try
        {
            if (!adapter.IsConfigured)
                throw new InvalidOperationException($"{adapter.DisplayName} ยังไม่ได้ตั้งค่า API — กรุณาใส่ค่าใน appsettings");

            var result = await adapter.FetchNewAsync(ct);

            foreach (var item in result.Items)
            {
                log.FetchedCount++;
                try
                {
                    var exists = await db.Complaints
                        .AnyAsync(c => c.ExternalSystem == systemKey && c.ExternalId == item.ExternalId, ct);

                    if (exists) { log.DuplicateCount++; continue; }

                    await ImportComplaintAsync(item, systemKey, ct);
                    log.NewCount++;
                }
                catch (Exception ex)
                {
                    log.ErrorCount++;
                    logger.LogWarning(ex, "Failed to import {ExternalId} from {System}", item.ExternalId, systemKey);
                }
            }

            log.SyncStatus    = "Success";
            log.CompletedAt   = DateTime.UtcNow;
            log.ErrorMessage  = result.ErrorMessage;
        }
        catch (Exception ex)
        {
            log.SyncStatus   = "Failed";
            log.CompletedAt  = DateTime.UtcNow;
            log.ErrorMessage = ex.Message;
            logger.LogError(ex, "Sync failed for {System}", systemKey);
        }

        await db.SaveChangesAsync(ct);
        return log;
    }

    public async Task<IReadOnlyList<ExternalSyncLog>> GetRecentLogsAsync(int count = 20, CancellationToken ct = default)
        => await db.ExternalSyncLogs
            .Include(l => l.TriggeredBy)
            .OrderByDescending(l => l.StartedAt)
            .Take(count)
            .ToListAsync(ct);

    public async Task<(bool Imported, string? ReferenceNumber)> ImportFromWebhookAsync(
        ExternalComplaintDto item, string systemKey, CancellationToken ct = default)
    {
        var exists = await db.Complaints
            .AnyAsync(c => c.ExternalSystem == systemKey && c.ExternalId == item.ExternalId, ct);
        if (exists) return (false, null);

        var complaint = await ImportComplaintAsync(item, systemKey, ct);
        return (true, complaint.ReferenceNumber);
    }

    public async Task<bool> ApplyStatusUpdateAsync(
        string systemKey, string externalId, string? newStatus, string? note, CancellationToken ct = default)
    {
        var complaint = await db.Complaints
            .FirstOrDefaultAsync(c => c.ExternalSystem == systemKey && c.ExternalId == externalId, ct);
        if (complaint == null) return false;

        if (newStatus != null && complaint.Status != newStatus)
        {
            var oldStatus = complaint.Status;
            complaint.Status    = newStatus;
            complaint.UpdatedAt = DateTime.UtcNow;
            if (newStatus == "Closed")
                complaint.ClosedAt = DateTime.UtcNow;

            db.ComplaintNotes.Add(new ComplaintNote
            {
                ComplaintId = complaint.Id,
                AuthorId    = null,
                NoteType    = "StatusChange",
                Content     = $"{oldStatus}→{newStatus} ({systemKey}: {note})",
                CreatedAt   = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }

        return true;
    }

    private async Task<Models.Complaint> ImportComplaintAsync(ExternalComplaintDto item, string systemKey, CancellationToken ct)
    {
        // Try to match the external system's category/type name against ours; fall back to
        // the first active category if there's no exact match.
        ComplaintCategory? category = null;
        if (!string.IsNullOrWhiteSpace(item.CategoryHint))
            category = await db.ComplaintCategories
                .FirstOrDefaultAsync(c => c.IsActive && c.Name == item.CategoryHint, ct);

        category ??= await db.ComplaintCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .FirstOrDefaultAsync(ct) ?? throw new InvalidOperationException("ไม่มีหมวดหมู่เรื่องร้องเรียน");

        var request = new SubmitComplaintRequest(
            ReporterName:   item.ReporterName ?? $"นำเข้าจาก {systemKey}",
            ReporterPhone:  item.ReporterPhone ?? "-",
            ReporterEmail:  null,
            ReporterIdCard: null,
            CategoryId:     category.Id,
            SubCategoryId:  null,
            SubjectStation: item.Address,
            IncidentDate:   item.IncidentDate.HasValue ? DateOnly.FromDateTime(item.IncidentDate.Value) : null,
            Description:    item.Description,
            Attachments:    Array.Empty<IFormFile>(),
            Latitude:       item.Latitude,
            Longitude:      item.Longitude
        );

        var complaint = await complaintService.SubmitAsync(request, ct);

        // ติด ExternalId และ Channel
        complaint.ExternalSystem = systemKey;
        complaint.ExternalId     = item.ExternalId;
        complaint.Channel        = systemKey;
        await db.SaveChangesAsync(ct);

        if (item.PhotoUrls is { Count: > 0 })
            await SaveExternalPhotosAsync(complaint.Id, complaint.ReferenceNumber, item.PhotoUrls, ct);

        return complaint;
    }

    /// <summary>Downloads externally-hosted photo URLs and
    /// stores them as ordinary local ComplaintAttachments, same as citizen-uploaded files —
    /// so they go through the same authorized /Staff/CaseDetail?handler=Attachment access path
    /// rather than linking out to a third-party host. Imports are capped at 3 photos.</summary>
    private async Task SaveExternalPhotosAsync(int complaintId, string refNumber, IReadOnlyList<string> photoUrls, CancellationToken ct)
    {
        var storagePath = config["FileUpload:StoragePath"] ?? Path.GetTempPath();
        var complaintDir = Path.Combine(storagePath, refNumber);
        Directory.CreateDirectory(complaintDir);
        var client = httpClientFactory.CreateClient();

        foreach (var url in photoUrls.Take(3))
        {
            try
            {
                using var response = await client.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode) continue;

                var contentType = response.Content.Headers.ContentType?.MediaType;
                if (contentType == null || !AllowedPhotoContentTypes.Contains(contentType))
                {
                    logger.LogWarning("Skipped external photo {Url} for complaint {ComplaintId}: unexpected content-type {ContentType}",
                        url, complaintId, contentType);
                    continue;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                var ext = contentType switch
                {
                    "image/jpeg" => ".jpg",
                    "image/png"  => ".png",
                    "image/gif"  => ".gif",
                    _            => ".jpg"
                };
                var storedName = $"{Guid.NewGuid()}{ext}";
                await File.WriteAllBytesAsync(Path.Combine(complaintDir, storedName), bytes, ct);

                db.ComplaintAttachments.Add(new ComplaintAttachment
                {
                    ComplaintId = complaintId,
                    FileName    = Path.GetFileName(new Uri(url).AbsolutePath) is { Length: > 0 } n ? n : storedName,
                    StoredPath  = Path.Combine(refNumber, storedName),
                    FileSize    = bytes.Length,
                    MimeType    = contentType,
                    UploadedAt  = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to download external photo {Url} for complaint {ComplaintId}", url, complaintId);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
