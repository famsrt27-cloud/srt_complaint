#nullable enable
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Filters;
using SRT.Complaint.Models;

namespace SRT.Complaint.Controllers.Api;

[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
[Route("api/v1/stats")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
[Produces("application/json")]
public class StatsController(AppDbContext db) : ControllerBase
{
    [HttpGet("summary", Name = "GetStatsSummaryV1")]
    [RequireScope("stats:summary")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var query = AccessibleComplaints();
        var summary = await query.GroupBy(_ => 1).Select(group => new StatsSummaryDto(
            group.Count(),
            group.Count(x => x.Status == "Pending"),
            group.Count(x => x.Status == "InProgress"),
            group.Count(x => x.Status == "Resolved"),
            group.Count(x => x.Status == "Closed"),
            group.Count(x => x.Status == "Rejected"),
            group.Count(x => x.SlaBreached),
            group.Count(x => x.CreatedAt >= now.Date)
        )).FirstOrDefaultAsync(ct) ?? new StatsSummaryDto(0, 0, 0, 0, 0, 0, 0, 0);

        return Ok(new
        {
            asOf = now,
            dataScope = CurrentApiKey.DataAccessMode == "All" ? "all" : "partner",
            complaints = summary
        });
    }

    [HttpGet("detailed", Name = "GetDetailedStatsV1")]
    [RequireScope("stats:detailed")]
    public async Task<IActionResult> GetDetailedStats(CancellationToken ct)
    {
        var query = AccessibleComplaints();
        var byCategory = await query.GroupBy(x => x.Category.Name)
            .Select(x => new { name = x.Key, count = x.Count() })
            .OrderByDescending(x => x.count).ToListAsync(ct);
        var byPriority = await query.GroupBy(x => x.Priority)
            .Select(x => new { name = x.Key, count = x.Count() })
            .OrderByDescending(x => x.count).ToListAsync(ct);
        var byStatus = await query.GroupBy(x => x.Status)
            .Select(x => new { name = x.Key, count = x.Count() })
            .OrderByDescending(x => x.count).ToListAsync(ct);
        var resolutionHours = await query
            .Where(x => x.ClosedAt != null)
            .Select(x => EF.Functions.DateDiffMinute(x.CreatedAt, x.ClosedAt!.Value) / 60.0)
            .ToListAsync(ct);

        return Ok(new
        {
            asOf = DateTime.UtcNow,
            dataScope = CurrentApiKey.DataAccessMode == "All" ? "all" : "partner",
            byCategory,
            byPriority,
            byStatus,
            averageResolutionHours = resolutionHours.Count == 0 ? (double?)null : Math.Round(resolutionHours.Average(), 2)
        });
    }

    private ApiKey CurrentApiKey => (HttpContext.Items["ApiKey"] as ApiKey)!;

    private IQueryable<Models.Complaint> AccessibleComplaints()
    {
        var key = CurrentApiKey;
        var query = db.Complaints.AsNoTracking();
        return key.DataAccessMode == "All"
            ? query
            : query.Where(x => x.SourcePartnerId == key.PartnerId);
    }
}

public record StatsSummaryDto(
    int Total,
    int Pending,
    int InProgress,
    int Resolved,
    int Closed,
    int Rejected,
    int SlaBreached,
    int TodayNew);
