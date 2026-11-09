using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public class SlaBackgroundService(IServiceScopeFactory scopeFactory, ILogger<SlaBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await RunChecksAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "SLA background check failed");
            }
        }
    }

    private static async Task RunChecksAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var notificationService = services.GetRequiredService<INotificationService>();
        var auditService = services.GetRequiredService<IAuditService>();
        var now = DateTime.UtcNow;

        // Assign unattended complaints after the configured threshold. The least-loaded
        // active general officer is selected to keep the queue balanced.
        var slaConfigs = await db.SlaConfigs.AsNoTracking().ToDictionaryAsync(x => x.Priority, ct);
        var pending = await db.Complaints
            .Where(c => c.AssignedToId == null && c.Status == "Pending")
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);
        var eligible = pending.Where(c => slaConfigs.TryGetValue(c.Priority, out var config) &&
                                          c.CreatedAt.AddHours(config.AutoAssignAfterHours) <= now)
                              .ToList();

        var officers = await db.StaffUsers
            .Where(x => x.IsActive && x.Role == "GeneralOfficer")
            .Select(x => new
            {
                User = x,
                ActiveCases = x.AssignedComplaints.Count(c =>
                    c.Status != "Closed" && c.Status != "Rejected")
            })
            .ToListAsync(ct);
        var loads = officers.ToDictionary(x => x.User.Id, x => x.ActiveCases);
        var autoAssigned = new List<(Models.Complaint Complaint, StaffUser Officer)>();
        foreach (var complaint in eligible)
        {
            var officer = officers.OrderBy(x => loads[x.User.Id]).ThenBy(x => x.User.Id).FirstOrDefault()?.User;
            if (officer == null) break;

            complaint.AssignedToId = officer.Id;
            complaint.AssignedAt = now;
            complaint.Status = "InProgress";
            complaint.WorkflowStep = ComplaintWorkflowSteps.Screening;
            complaint.WorkflowUpdatedAt = now;
            complaint.UpdatedAt = now;
            db.ComplaintNotes.Add(new ComplaintNote
            {
                Complaint = complaint,
                AuthorId = null,
                NoteType = "System",
                Content = $"ระบบมอบหมายอัตโนมัติให้ {officer.FullName} ตามเกณฑ์ SLA",
                CreatedAt = now
            });
            loads[officer.Id]++;
            autoAssigned.Add((complaint, officer));
        }

        if (autoAssigned.Count > 0)
            await db.SaveChangesAsync(ct);

        foreach (var item in autoAssigned)
        {
            await auditService.LogAsync("AutoAssignComplaint", null, "SYSTEM", "Complaint",
                item.Complaint.Id.ToString(), new { item.Complaint.ReferenceNumber, assignedToId = item.Officer.Id }, null, ct);
            if (!string.IsNullOrWhiteSpace(item.Officer.Email))
            {
                await notificationService.SendAsync("AutoAssigned", null, item.Officer.Email, new()
                {
                    ["ReferenceNumber"] = item.Complaint.ReferenceNumber
                }, ct);
            }
        }

        // Send one warning per complaint when the configured percentage of the SLA
        // duration has elapsed (for example 80% elapsed = 20% remaining).
        var warningCandidates = await db.Complaints
            .Include(c => c.AssignedTo)
            .Where(c => c.SlaWarningSentAt == null && !c.SlaBreached && c.SlaDeadline.HasValue &&
                        c.Status != "Closed" && c.Status != "Rejected")
            .ToListAsync(ct);
        var warnings = warningCandidates.Where(c =>
        {
            if (!slaConfigs.TryGetValue(c.Priority, out var sla)) return c.SlaDeadline <= now.AddHours(24);
            var warningWindowHours = Math.Max(1, sla.ResolutionHours * (100 - sla.WarningThresholdPercent) / 100d);
            return c.SlaDeadline <= now.AddHours(warningWindowHours);
        }).ToList();

        foreach (var complaint in warnings)
            complaint.SlaWarningSentAt = now;
        if (warnings.Count > 0)
            await db.SaveChangesAsync(ct);

        foreach (var complaint in warnings)
        {
            if (string.IsNullOrWhiteSpace(complaint.AssignedTo?.Email)) continue;
            await notificationService.SendAsync("SlaWarning", null, complaint.AssignedTo.Email, new()
            {
                ["ReferenceNumber"] = complaint.ReferenceNumber
            }, ct);
        }

        // Mark SLA breached
        var breached = await db.Complaints
            .Include(c => c.AssignedTo)
            .Where(c => !c.SlaBreached && c.SlaDeadline.HasValue && c.SlaDeadline < now
                        && c.Status != "Closed" && c.Status != "Rejected")
            .ToListAsync(ct);
        foreach (var c in breached)
            c.SlaBreached = true;

        if (breached.Count > 0)
            await db.SaveChangesAsync(ct);

        // Notify the assigned officer that SLA has been breached (idempotent — SlaBreached flag prevents repeat sends)
        foreach (var c in breached)
        {
            if (string.IsNullOrEmpty(c.AssignedTo?.Email)) continue;
            await notificationService.SendAsync("SlaBreached", null, c.AssignedTo.Email, new()
            {
                ["ReferenceNumber"] = c.ReferenceNumber
            }, ct);
        }
    }
}
