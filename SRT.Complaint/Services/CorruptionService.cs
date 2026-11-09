using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Models;
using SRT.Complaint.Models.Corruption;

namespace SRT.Complaint.Services;

public class CorruptionService(
    CorruptionDbContext db,
    IMaskingService masking,
    ISlaService slaService,
    INotificationService notificationService,
    IAuditService auditService,
    IConfiguration config,
    AppDbContext appDb) : ICorruptionService
{
    public async Task<CorruptionReport> SubmitAsync(SubmitCorruptionRequest request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var refNum = await GenerateReferenceNumberAsync(ct);

        var report = new CorruptionReport
        {
            ReferenceNumber = refNum,
            ReporterNameEncrypted = masking.Encrypt(request.ReporterName),
            ReporterPhoneEncrypted = masking.Encrypt(request.ReporterPhone),
            ReporterEmailEncrypted = request.ReporterEmail is null ? null : masking.Encrypt(request.ReporterEmail),
            ReporterIdCardEncrypted = masking.Encrypt(request.ReporterIdCard),
            ReporterNameMasked = masking.MaskName(request.ReporterName),
            ReporterPhoneMasked = masking.MaskPhone(request.ReporterPhone),
            ReporterEmailMasked = masking.MaskEmail(request.ReporterEmail),
            SubjectType = request.SubjectType,
            SubjectPersonName = request.SubjectPersonName,
            SubjectDepartment = request.SubjectDepartment,
            IncidentDate = request.IncidentDate,
            Description = request.Description,
            Priority = "Normal",
            Status = "Pending",
            SlaDeadline = await slaService.CalculateDeadlineAsync("Normal", now),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Reports.Add(report);
        await db.SaveChangesAsync(ct);

        await SaveAttachmentsAsync(report, request.Attachments, ct);

        await auditService.LogAsync("CorruptionReportSubmitted", null, null, "CorruptionReport", report.Id.ToString(), new { refNum }, null, ct);

        await notificationService.SendAsync("ComplaintReceived", request.ReporterPhone, request.ReporterEmail, new()
        {
            ["ReferenceNumber"] = refNum,
            ["TrackingUrl"] = $"https://www.railway.co.th/complaint/track/{refNum}"
        }, ct);

        return report;
    }

    public async Task<CorruptionReport?> GetByReferenceAsync(string referenceNumber, CancellationToken ct = default)
        => await db.Reports.FirstOrDefaultAsync(r => r.ReferenceNumber == referenceNumber, ct);

    public async Task<CorruptionReport?> GetByIdAsync(int id, CancellationToken ct = default)
        => await db.Reports
            .Include(r => r.InvestigationLogs)
            .Include(r => r.DecryptionLogs)
            .Include(r => r.Attachments)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task UpdateStatusAsync(int id, string newStatus, int actorId, string? note, CancellationToken ct = default)
    {
        var report = await db.Reports.FindAsync([id], ct)
            ?? throw new InvalidOperationException("Report not found");
        if (!CorruptionStatusTransitionPolicy.CanTransition(report.Status, newStatus))
            throw new InvalidOperationException($"Cannot transition corruption report from {report.Status} to {newStatus}");

        var oldStatus = report.Status;
        report.Status = newStatus;
        report.UpdatedAt = DateTime.UtcNow;
        if (newStatus is "Closed") report.ClosedAt = DateTime.UtcNow;
        if (newStatus is not "Closed") report.ClosedAt = null;
        if (!string.IsNullOrWhiteSpace(note))
        {
            db.InvestigationLogs.Add(new InvestigationLog
            {
                ReportId = id,
                AuthorId = actorId,
                Content = note.Trim(),
                IsConfidential = true,
                CreatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync(ct);
        await auditService.LogAsync("UpdateCorruptionStatus", actorId, null, "CorruptionReport", id.ToString(), new { oldStatus, newStatus, hasNote = !string.IsNullOrWhiteSpace(note) }, null, ct);
    }

    public async Task ClaimAsync(int id, int staffId, CancellationToken ct = default)
    {
        var report = await db.Reports.FindAsync([id], ct)
            ?? throw new InvalidOperationException("Report not found");
        if (report.Status != "Pending") throw new InvalidOperationException("Only pending reports can be claimed");
        if (report.AssignedToId.HasValue) throw new InvalidOperationException("Already claimed");
        report.AssignedToId = staffId;
        report.AssignedAt = DateTime.UtcNow;
        report.Status = "InProgress";
        report.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await auditService.LogAsync("ClaimCorruptionCase", staffId, null, "CorruptionReport", id.ToString(), null, null, ct);
    }

    public async Task CloseAsync(int id, string resolutionNote, int actorId, CancellationToken ct = default)
        => await UpdateStatusAsync(id, "Closed", actorId, resolutionNote, ct);

    public async Task ReopenAsync(int id, int actorId, CancellationToken ct = default)
    {
        var report = await db.Reports.FindAsync([id], ct)
            ?? throw new InvalidOperationException("Report not found");
        if (report.Status != "Closed") throw new InvalidOperationException("Only closed reports can be reopened");

        await UpdateStatusAsync(id, "InProgress", actorId, "เปิดเรื่องใหม่เพื่อสืบสวนเพิ่มเติม", ct);
        await auditService.LogAsync("ReopenCorruptionCase", actorId, null, "CorruptionReport", id.ToString(), null, null, ct);
    }

    public async Task<DecryptedReporterInfo> DecryptReporterInfoAsync(int reportId, int requestedById, string reason, string ipAddress, CancellationToken ct = default)
    {
        var report = await db.Reports.FindAsync([reportId], ct)
            ?? throw new InvalidOperationException("Report not found");

        db.DecryptionLogs.Add(new DecryptionLog
        {
            ReportId = reportId,
            RequestedById = requestedById,
            Reason = reason,
            IpAddress = ipAddress,
            RequestedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        await auditService.LogAsync("DecryptReporterInfo", requestedById, null, "CorruptionReport", reportId.ToString(), new { reason }, ipAddress, ct);

        return new DecryptedReporterInfo(
            masking.Decrypt(report.ReporterNameEncrypted),
            masking.Decrypt(report.ReporterPhoneEncrypted),
            report.ReporterEmailEncrypted is null ? null : masking.Decrypt(report.ReporterEmailEncrypted),
            masking.Decrypt(report.ReporterIdCardEncrypted)
        );
    }

    public async Task<IReadOnlyList<CorruptionReport>> GetQueueAsync(int page, int pageSize, CancellationToken ct = default)
        => await db.Reports
            .OrderBy(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CorruptionReport>> GetQueueFilteredAsync(CorruptionQueueFilter filter, CancellationToken ct = default)
    {
        var q = ApplyFilter(db.Reports.AsQueryable(), filter);
        return await q
            .OrderByDescending(r => r.SlaBreached)
            .ThenBy(r => r.SlaDeadline)
            .ThenBy(r => r.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
    }

    public async Task<int> GetTotalCountAsync(CorruptionQueueFilter filter, CancellationToken ct = default)
        => await ApplyFilter(db.Reports.AsQueryable(), filter).CountAsync(ct);

    public async Task AddInvestigationLogAsync(int reportId, int authorId, string content, bool isConfidential, CancellationToken ct = default)
    {
        db.InvestigationLogs.Add(new InvestigationLog
        {
            ReportId = reportId,
            AuthorId = authorId,
            Content = content,
            IsConfidential = isConfidential,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
        await auditService.LogAsync("AddInvestigationLog", authorId, null, "CorruptionReport", reportId.ToString(), new { isConfidential }, null, ct);
    }

    public async Task<CorruptionDashboardStats> GetDashboardStatsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var yesterdayStart = todayStart.AddDays(-1);
        var sevenDaysAgo = todayStart.AddDays(-6);
        var normalSla = await appDb.SlaConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.Priority == "Normal", ct);
        var warningWindowHours = normalSla is null
            ? 24
            : Math.Max(1, normalSla.ResolutionHours * (100 - normalSla.WarningThresholdPercent) / 100d);
        var slaWarnBefore = now.AddHours(warningWindowHours);
        var thaiCulture = new System.Globalization.CultureInfo("th-TH");

        var todayCount = await db.Reports.CountAsync(r => r.CreatedAt >= todayStart, ct);
        var yesterdayCount = await db.Reports.CountAsync(r => r.CreatedAt >= yesterdayStart && r.CreatedAt < todayStart, ct);

        var activeStatuses = new[] { "Pending", "InProgress", "UnderReview" };
        var activeReports = await db.Reports
            .Where(r => activeStatuses.Contains(r.Status))
            .Select(r => new { r.Status, r.SlaDeadline, r.SlaBreached })
            .ToListAsync(ct);

        var pendingCount    = activeReports.Count(r => r.Status == "Pending");
        var inProgressCount = activeReports.Count(r => r.Status is "InProgress" or "UnderReview");
        var slaBreachedCount = activeReports.Count(r => r.SlaBreached);
        var slaWarningCount = activeReports.Count(r =>
            !r.SlaBreached && r.SlaDeadline.HasValue && r.SlaDeadline <= slaWarnBefore);

        var recentDates = await db.Reports
            .Where(r => r.CreatedAt >= sevenDaysAgo)
            .Select(r => r.CreatedAt)
            .ToListAsync(ct);

        var chartLabels = new List<string>();
        var chartData   = new List<int>();
        for (var i = 6; i >= 0; i--)
        {
            var day = todayStart.AddDays(-i);
            chartLabels.Add(day.ToString("d MMM", thaiCulture));
            chartData.Add(recentDates.Count(d => d.Date == day.Date));
        }

        var warningReports = await db.Reports
            .Where(r => (r.SlaBreached || (r.SlaDeadline.HasValue && r.SlaDeadline <= slaWarnBefore))
                     && activeStatuses.Contains(r.Status))
            .OrderBy(r => r.SlaDeadline)
            .Take(10)
            .ToListAsync(ct);

        var slaItems = warningReports.Select(r =>
        {
            string remaining;
            if (r.SlaBreached || (r.SlaDeadline.HasValue && r.SlaDeadline < now))
                remaining = "เกิน SLA แล้ว";
            else if (r.SlaDeadline.HasValue)
            {
                var diff = r.SlaDeadline.Value - now;
                remaining = diff.TotalHours < 1
                    ? $"เหลือ {(int)diff.TotalMinutes} นาที"
                    : $"เหลือ {(int)diff.TotalHours} ชั่วโมง";
            }
            else remaining = "-";

            return new CorruptionSlaItem(
                r.Id, r.ReferenceNumber, r.SubjectType, r.SubjectPersonName,
                r.SlaBreached || (r.SlaDeadline.HasValue && r.SlaDeadline < now), remaining);
        }).ToList();

        return new CorruptionDashboardStats(
            todayCount, yesterdayCount, pendingCount, inProgressCount,
            slaWarningCount, slaBreachedCount, chartLabels, chartData, slaItems);
    }

    private static IQueryable<CorruptionReport> ApplyFilter(IQueryable<CorruptionReport> q, CorruptionQueueFilter filter)
    {
        if (!string.IsNullOrEmpty(filter.Status)) q = q.Where(r => r.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            q = q.Where(r => r.ReferenceNumber.Contains(search) ||
                             r.SubjectType.Contains(search) ||
                             (r.SubjectPersonName != null && r.SubjectPersonName.Contains(search)) ||
                             (r.SubjectDepartment != null && r.SubjectDepartment.Contains(search)));
        }
        q = filter.QuickView switch
        {
            "mine" when filter.ViewerStaffId.HasValue => q.Where(r => r.AssignedToId == filter.ViewerStaffId),
            "unassigned" => q.Where(r => r.AssignedToId == null && r.Status == "Pending"),
            "sla-breached" => q.Where(r => r.SlaBreached || (r.SlaDeadline.HasValue && r.SlaDeadline < DateTime.UtcNow)),
            "review" => q.Where(r => r.Status == "UnderReview"),
            _ => q
        };
        return q;
    }

    private async Task<string> GenerateReferenceNumberAsync(CancellationToken ct)
    {
        var thYear = DateTime.UtcNow.Year + 543;
        var seq = await NextSequenceAsync(ct);
        return $"SRT-CORUPT-{thYear}-{seq:D4}";
    }

    private async Task<long> NextSequenceAsync(CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT NEXT VALUE FOR corruption.CorruptionSeq";
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    }

    private async Task SaveAttachmentsAsync(CorruptionReport report, IReadOnlyList<IFormFile> files, CancellationToken ct)
    {
        if (files.Count == 0) return;

        var storageRoot = config["FileUpload:StoragePath"] ?? Path.GetTempPath();
        var relativeDirectory = Path.Combine("corruption", report.ReferenceNumber);
        var reportDirectory = Path.Combine(storageRoot, relativeDirectory);
        Directory.CreateDirectory(reportDirectory);

        foreach (var file in files.Where(x => x.Length > 0))
        {
            var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
            await using var stream = File.Create(Path.Combine(reportDirectory, storedName));
            await file.CopyToAsync(stream, ct);
            report.Attachments.Add(new CorruptionAttachment
            {
                FileName = Path.GetFileName(file.FileName),
                StoredPath = Path.Combine(relativeDirectory, storedName),
                FileSize = file.Length,
                MimeType = file.ContentType,
                UploadedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
