#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SRT.Complaint.Data;
using SRT.Complaint.Models;
using SRT.Complaint.Validation;

namespace SRT.Complaint.Services;

public sealed class EdocIntegrationService(
    AppDbContext db,
    IEdocApiClient apiClient,
    IOptions<EdocOptions> options,
    IConfiguration configuration,
    IAuditService auditService,
    ILogger<EdocIntegrationService> logger) : IEdocIntegrationService
{
    private readonly EdocOptions _options = options.Value;

    public bool IsEnabled => _options.Enabled && apiClient.IsConfigured;

    public IReadOnlyList<EdocBasketOption> GetBaskets()
    {
        var baskets = _options.Baskets
            .Select(x => new EdocBasketOption { Id = x.Id, Name = x.Name })
            .ToList();
        if (!string.IsNullOrWhiteSpace(_options.BasketId) && baskets.All(x => x.Id != _options.BasketId))
            baskets.Insert(0, new EdocBasketOption { Id = _options.BasketId, Name = _options.FromName });
        return baskets;
    }

    public string? GetDefaultBasketId(int categoryId)
    {
        if (_options.CategoryBasketMappings.TryGetValue(categoryId.ToString(), out var mapped)) return mapped;
        if (!string.IsNullOrWhiteSpace(_options.BasketId)) return _options.BasketId;
        return GetBaskets().FirstOrDefault()?.Id;
    }

    public Task<EdocDocumentLink?> GetByComplaintIdAsync(int complaintId, CancellationToken ct = default) =>
        db.EdocDocumentLinks.AsNoTracking()
            .Include(x => x.AttachmentDeliveries)
            .FirstOrDefaultAsync(x => x.ComplaintId == complaintId, ct);

    public async Task<EdocDocumentLink> QueueAsync(
        int complaintId,
        int createdById,
        string? basketId = null,
        CancellationToken ct = default)
    {
        EnsureEnabled();
        var existing = await db.EdocDocumentLinks
            .Include(x => x.AttachmentDeliveries)
            .FirstOrDefaultAsync(x => x.ComplaintId == complaintId, ct);
        if (existing != null) return existing;

        var complaint = await db.Complaints.AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Attachments)
            .FirstOrDefaultAsync(x => x.Id == complaintId, ct)
            ?? throw new InvalidOperationException("Complaint not found");
        ValidateComplaintMapping(complaint);
        var selectedBasketId = string.IsNullOrWhiteSpace(basketId)
            ? GetDefaultBasketId(complaint.CategoryId)
            : basketId.Trim();
        var basket = GetBaskets().FirstOrDefault(x => x.Id == selectedBasketId)
            ?? throw new InvalidOperationException("Basket eDOC ที่เลือกไม่ได้อยู่ในรายการที่ผู้ดูแลกำหนด");

        var link = new EdocDocumentLink
        {
            ComplaintId = complaint.Id,
            CreatedById = createdById,
            WID = Limit(complaint.ReferenceNumber, 100),
            BasketId = basket.Id,
            BasketName = Limit(basket.Name, 200),
            SyncStatus = EdocSyncStatuses.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            AttachmentDeliveries = complaint.Attachments.Select(x => new EdocAttachmentDelivery
            {
                ComplaintAttachmentId = x.Id,
                Status = "Pending"
            }).ToList()
        };
        db.EdocDocumentLinks.Add(link);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var concurrent = await db.EdocDocumentLinks
                .Include(x => x.AttachmentDeliveries)
                .FirstOrDefaultAsync(x => x.ComplaintId == complaintId, ct);
            if (concurrent != null) return concurrent;
            throw;
        }
        await auditService.LogAsync("QueueEdocDocument", createdById, null, "Complaint", complaintId.ToString(),
            new { link.WID, link.BasketId, Attachments = link.AttachmentDeliveries.Count }, null, ct);
        return link;
    }

    public async Task<EdocDocumentLink> SendNowAsync(int complaintId, CancellationToken ct = default)
    {
        var linkId = await db.EdocDocumentLinks.Where(x => x.ComplaintId == complaintId)
            .Select(x => x.Id).FirstOrDefaultAsync(ct);
        if (linkId == 0) throw new InvalidOperationException("eDOC document has not been queued");
        await ProcessLinkAsync(linkId, ct);
        return await GetByComplaintIdAsync(complaintId, ct)
            ?? throw new InvalidOperationException("eDOC document link not found after processing");
    }

    public async Task<int> ProcessPendingAsync(int maxItems = 5, CancellationToken ct = default)
    {
        if (!IsEnabled) return 0;
        var ids = await db.EdocDocumentLinks.AsNoTracking()
            .Where(x => x.SyncStatus == EdocSyncStatuses.Pending)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Id)
            .Take(Math.Clamp(maxItems, 1, 20))
            .ToListAsync(ct);

        var processed = 0;
        foreach (var id in ids)
        {
            if (await ProcessLinkAsync(id, ct)) processed++;
        }
        return processed;
    }

    public async Task RecoverStaleAsync(CancellationToken ct = default)
    {
        if (!IsEnabled) return;
        var now = DateTime.UtcNow;
        await db.EdocDocumentLinks
            .Where(x => x.SyncStatus == EdocSyncStatuses.Sending && x.LeaseExpiresAt < now)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.SyncStatus, EdocSyncStatuses.NeedsReview)
                .SetProperty(x => x.LastError, "Worker stopped while sending. Reconcile with eDOC before retrying.")
                .SetProperty(x => x.LeaseExpiresAt, (DateTime?)null)
                .SetProperty(x => x.UpdatedAt, now), ct);
    }

    public async Task<EdocDocumentLink> ReconcileAsync(int complaintId, CancellationToken ct = default)
    {
        EnsureEnabled();
        var link = await db.EdocDocumentLinks
            .Include(x => x.Complaint).ThenInclude(x => x.Category)
            .Include(x => x.AttachmentDeliveries).ThenInclude(x => x.ComplaintAttachment)
            .FirstOrDefaultAsync(x => x.ComplaintId == complaintId, ct)
            ?? throw new InvalidOperationException("eDOC document link not found");
        var inquiry = await apiClient.InquiryAsync(link.WID, link.BasketId, ct);
        link.LastTrackedAt = DateTime.UtcNow;
        link.NextTrackingAt = DateTime.UtcNow.AddMinutes(_options.TrackingIntervalMinutes);
        link.UpdatedAt = DateTime.UtcNow;
        if (inquiry.Found)
        {
            link.RegisterNumber ??= Limit(inquiry.RegisterNumber, 20);
            link.RemoteStatusDescription = Limit(inquiry.StatusDescription, 2000);
            if (link.SyncStatus is EdocSyncStatuses.NeedsReview or EdocSyncStatuses.Failed)
            {
                link.SyncStatus = EdocSyncStatuses.Sent;
                link.SentAt ??= DateTime.UtcNow;
                link.LastError = null;
            }
            await db.SaveChangesAsync(ct);
            var attachmentErrors = await UploadPendingAttachmentsAsync(link, ct);
            if (attachmentErrors > 0)
            {
                link.SyncStatus = EdocSyncStatuses.SentWithAttachmentErrors;
                link.LastError = $"Document exists, but {attachmentErrors} attachment(s) require review.";
            }
        }
        else
        {
            link.LastError = "eDOC inquiry did not find this WID. Review the mapping before retrying.";
        }
        await db.SaveChangesAsync(ct);
        return link;
    }

    public async Task<EdocDocumentLink> TrackAsync(int complaintId, CancellationToken ct = default)
    {
        EnsureEnabled();
        var link = await db.EdocDocumentLinks.FirstOrDefaultAsync(x => x.ComplaintId == complaintId, ct)
            ?? throw new InvalidOperationException("eDOC document link not found");
        await TrackLinkAsync(link, ct);
        return link;
    }

    public async Task<int> TrackDueAsync(int maxItems = 20, CancellationToken ct = default)
    {
        if (!IsEnabled) return 0;
        var now = DateTime.UtcNow;
        var links = await db.EdocDocumentLinks
            .Where(x => (x.SyncStatus == EdocSyncStatuses.Sent ||
                         x.SyncStatus == EdocSyncStatuses.SentWithAttachmentErrors) &&
                        (x.NextTrackingAt == null || x.NextTrackingAt <= now))
            .OrderBy(x => x.NextTrackingAt)
            .Take(Math.Clamp(maxItems, 1, 100))
            .ToListAsync(ct);
        foreach (var link in links)
        {
            try { await TrackLinkAsync(link, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Unable to track eDOC WID {WID}", link.WID); }
        }
        return links.Count;
    }

    public async Task RetryAsync(int complaintId, int actorId, CancellationToken ct = default)
    {
        var link = await db.EdocDocumentLinks.FirstOrDefaultAsync(x => x.ComplaintId == complaintId, ct)
            ?? throw new InvalidOperationException("eDOC document link not found");
        if (link.SyncStatus is not EdocSyncStatuses.Failed and not EdocSyncStatuses.NeedsReview)
            throw new InvalidOperationException("Only failed or review-required eDOC jobs can be retried");

        link.SyncStatus = EdocSyncStatuses.Pending;
        link.LastError = null;
        link.LeaseExpiresAt = null;
        link.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await auditService.LogAsync("RetryEdocDocument", actorId, null, "Complaint", complaintId.ToString(),
            new { link.WID }, null, ct);
    }

    public async Task<EdocDocumentLink> RetryAttachmentsAsync(int complaintId, int actorId, CancellationToken ct = default)
    {
        EnsureEnabled();
        var link = await db.EdocDocumentLinks
            .Include(x => x.Complaint).ThenInclude(x => x.Category)
            .Include(x => x.AttachmentDeliveries).ThenInclude(x => x.ComplaintAttachment)
            .FirstOrDefaultAsync(x => x.ComplaintId == complaintId, ct)
            ?? throw new InvalidOperationException("eDOC document link not found");
        if (link.SyncStatus != EdocSyncStatuses.SentWithAttachmentErrors)
            throw new InvalidOperationException("Only eDOC documents with attachment errors can retry attachments");

        foreach (var delivery in link.AttachmentDeliveries.Where(x => x.Status == "NeedsReview"))
        {
            delivery.Status = "Pending";
            delivery.LastError = null;
        }
        await db.SaveChangesAsync(ct);

        var errors = await UploadPendingAttachmentsAsync(link, ct);
        link.SyncStatus = errors == 0 ? EdocSyncStatuses.Sent : EdocSyncStatuses.SentWithAttachmentErrors;
        link.LastError = errors == 0 ? null : $"Document exists, but {errors} attachment(s) still require review.";
        link.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await auditService.LogAsync("RetryEdocAttachments", actorId, null, "Complaint", complaintId.ToString(),
            new { link.WID, Errors = errors }, null, ct);
        return link;
    }

    private async Task<bool> ProcessLinkAsync(int linkId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        int claimed;
        if (db.Database.IsRelational())
        {
            claimed = await db.EdocDocumentLinks
                .Where(x => x.Id == linkId && x.SyncStatus == EdocSyncStatuses.Pending)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(x => x.SyncStatus, EdocSyncStatuses.Sending)
                    .SetProperty(x => x.LeaseExpiresAt, now.AddMinutes(3))
                    .SetProperty(x => x.LastAttemptAt, now)
                    .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                    .SetProperty(x => x.UpdatedAt, now), ct);
        }
        else
        {
            var pending = await db.EdocDocumentLinks
                .FirstOrDefaultAsync(x => x.Id == linkId && x.SyncStatus == EdocSyncStatuses.Pending, ct);
            claimed = pending == null ? 0 : 1;
            if (pending != null)
            {
                pending.SyncStatus = EdocSyncStatuses.Sending;
                pending.LeaseExpiresAt = now.AddMinutes(3);
                pending.LastAttemptAt = now;
                pending.AttemptCount++;
                pending.UpdatedAt = now;
                await db.SaveChangesAsync(ct);
            }
        }
        if (claimed == 0) return false;

        db.ChangeTracker.Clear();
        var link = await db.EdocDocumentLinks
            .Include(x => x.Complaint).ThenInclude(x => x.Category)
            .Include(x => x.Complaint).ThenInclude(x => x.Attachments)
            .Include(x => x.AttachmentDeliveries).ThenInclude(x => x.ComplaintAttachment)
            .FirstAsync(x => x.Id == linkId, ct);

        try
        {
            var createResult = await apiClient.CreateDocumentAsync(BuildCreateCommand(link.Complaint, link.BasketId), ct);
            link.RegisterNumber = Limit(createResult.RegisterNumber, 20);
            link.RemoteDocumentDate = Limit(createResult.DocumentDate, 50);
            link.SyncStatus = EdocSyncStatuses.Sent;
            link.SentAt = DateTime.UtcNow;
            link.NextTrackingAt = DateTime.UtcNow.AddMinutes(_options.TrackingIntervalMinutes);
            link.LeaseExpiresAt = null;
            link.LastError = null;
            link.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var attachmentErrors = await UploadPendingAttachmentsAsync(link, ct);
            if (attachmentErrors > 0)
            {
                link.SyncStatus = EdocSyncStatuses.SentWithAttachmentErrors;
                link.LastError = $"Document created, but {attachmentErrors} attachment(s) require review.";
            }
            link.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await auditService.LogAsync("CreateEdocDocument", link.CreatedById, null, "Complaint", link.ComplaintId.ToString(),
                new { link.WID, link.RegisterNumber, AttachmentErrors = attachmentErrors }, null, ct);
            return true;
        }
        catch (EdocApiException ex)
        {
            link.SyncStatus = ex.AmbiguousOutcome ? EdocSyncStatuses.NeedsReview : EdocSyncStatuses.Failed;
            link.LastError = Limit(Describe(ex), 2000);
            link.LeaseExpiresAt = null;
            link.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogWarning(ex, "eDOC create failed for complaint {ComplaintId}; ambiguous={Ambiguous}",
                link.ComplaintId, ex.AmbiguousOutcome);
            return true;
        }
        catch (Exception ex)
        {
            link.SyncStatus = EdocSyncStatuses.NeedsReview;
            link.LastError = "Unexpected failure while sending to eDOC. Reconcile before retrying.";
            link.LeaseExpiresAt = null;
            link.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            logger.LogError(ex, "Unexpected eDOC failure for complaint {ComplaintId}", link.ComplaintId);
            return true;
        }
    }

    private async Task<int> UploadPendingAttachmentsAsync(EdocDocumentLink link, CancellationToken ct)
    {
        var errors = 0;
        var storageRoot = configuration["FileUpload:StoragePath"] ?? Path.GetTempPath();
        foreach (var delivery in link.AttachmentDeliveries.Where(x => x.Status == "Pending"))
        {
            delivery.AttemptCount++;
            delivery.LastAttemptAt = DateTime.UtcNow;
            var attachment = delivery.ComplaintAttachment;
            try
            {
                var fullPath = FileStorage.ResolveWithinRoot(storageRoot, attachment.StoredPath);
                if (fullPath == null || !File.Exists(fullPath))
                    throw new InvalidOperationException("Attachment file was not found in approved storage");
                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length > _options.MaxAttachmentBytes)
                    throw new InvalidOperationException($"Attachment exceeds the eDOC limit of {_options.MaxAttachmentBytes} bytes");

                var bytes = await File.ReadAllBytesAsync(fullPath, ct);
                var extension = Path.GetExtension(attachment.FileName).TrimStart('.').ToLowerInvariant();
                if (extension.Length is < 1 or > 4)
                    throw new InvalidOperationException("Attachment extension must contain 1-4 characters for eDOC");
                var baseName = Limit(Path.GetFileNameWithoutExtension(attachment.FileName), 100 - extension.Length - 1);
                var result = await apiClient.UploadDocumentAsync(new EdocUploadCommand(
                    link.WID,
                    link.BasketId,
                    link.WID,
                    baseName,
                    extension,
                    Convert.ToBase64String(bytes),
                    Limit($"เอกสารแนบเรื่อง {link.Complaint.ReferenceNumber}", 100)), ct);
                if (!result.Success) throw new InvalidOperationException("eDOC did not accept the attachment");

                delivery.Status = "Uploaded";
                delivery.UploadedAt = DateTime.UtcNow;
                delivery.LastError = null;
            }
            catch (Exception ex)
            {
                errors++;
                delivery.Status = "NeedsReview";
                delivery.LastError = Limit(ex is EdocApiException apiEx ? Describe(apiEx) : ex.Message, 2000);
                logger.LogWarning(ex, "Unable to upload attachment {AttachmentId} to eDOC", attachment.Id);
            }
            await db.SaveChangesAsync(ct);
        }
        return errors;
    }

    private async Task TrackLinkAsync(EdocDocumentLink link, CancellationToken ct)
    {
        var result = await apiClient.TrackingAsync(link.WID, ct);
        var latest = result.Entries.LastOrDefault();
        var previousCode = link.RemoteStatusCode;
        link.RemoteStatusCode = Limit(latest?.StatusCode, 20);
        link.RemoteStatusDescription = Limit(latest?.ActionMessage, 2000);
        link.LastTrackedAt = DateTime.UtcNow;
        link.NextTrackingAt = DateTime.UtcNow.AddMinutes(_options.TrackingIntervalMinutes);
        link.UpdatedAt = DateTime.UtcNow;
        link.LastError = null;

        if (latest?.StatusCode != null && latest.StatusCode != previousCode)
        {
            db.ComplaintNotes.Add(new ComplaintNote
            {
                ComplaintId = link.ComplaintId,
                AuthorId = null,
                NoteType = "Integration",
                Content = $"eDOC status: {latest.StatusCode} - {Limit(latest.ActionMessage, 500)}",
                CreatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private EdocCreateCommand BuildCreateCommand(Models.Complaint complaint, string basketId)
    {
        ValidateComplaintMapping(complaint);
        var sendTo = !string.IsNullOrWhiteSpace(complaint.ResponsibleDepartment)
            ? complaint.ResponsibleDepartment
            : !string.IsNullOrWhiteSpace(complaint.Category.DepartmentName)
                ? complaint.Category.DepartmentName
                : _options.DefaultSendTo;
        var subject = $"เรื่องร้องเรียน {complaint.ReferenceNumber} - {complaint.Category.Name}";
        return new EdocCreateCommand(
            Limit(complaint.ReferenceNumber, 100),
            basketId,
            Limit(complaint.ExternalId ?? complaint.ReferenceNumber, 100),
            Limit(sendTo, 100),
            Limit(subject, 255),
            FormatDocumentDate(complaint.CreatedAt),
            MapPriority(complaint.Priority),
            Limit(complaint.Description, 2000),
            Limit($"อ้างอิงเรื่องร้องเรียน {complaint.ReferenceNumber}", 300));
    }

    private void ValidateComplaintMapping(Models.Complaint complaint)
    {
        var sendTo = complaint.ResponsibleDepartment ?? complaint.Category?.DepartmentName;
        if (string.IsNullOrWhiteSpace(sendTo) && string.IsNullOrWhiteSpace(_options.DefaultSendTo))
            throw new InvalidOperationException("กรุณากำหนดหน่วยงานปลายทางในหมวดเรื่องหรือ Edoc:DefaultSendTo ก่อนส่ง eDOC");
        if (string.IsNullOrWhiteSpace(complaint.Description))
            throw new InvalidOperationException("Complaint description is required for eDOC");
    }

    private string FormatDocumentDate(DateTime utcDate)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDate, DateTimeKind.Utc), BangkokTimeZone);
        var year = _options.UseBuddhistYear ? local.Year + 543 : local.Year;
        return $"{year:0000}{_options.DocumentDateSeparator}{local.Month:00}{_options.DocumentDateSeparator}{local.Day:00}";
    }

    private static string MapPriority(string priority) => priority switch
    {
        "Critical" => "03",
        "Urgent" => "02",
        "High" => "01",
        _ => "00"
    };

    private static string Describe(EdocApiException ex) =>
        ex.StatusCode.HasValue
            ? $"{ex.Message}; HTTP={ex.StatusCode}; RemoteCode={ex.RemoteCode ?? "-"}"
            : ex.Message;

    private static string Limit(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private void EnsureEnabled()
    {
        if (!IsEnabled) throw new InvalidOperationException("eDOC integration is not enabled or configured");
    }

    private static readonly TimeZoneInfo BangkokTimeZone = ResolveBangkokTimeZone();

    private static TimeZoneInfo ResolveBangkokTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Bangkok" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.CreateCustomTimeZone("Asia/Bangkok", TimeSpan.FromHours(7), "Bangkok", "Bangkok");
    }
}
