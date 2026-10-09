#nullable enable
using Microsoft.AspNetCore.Mvc;
using SRT.Complaint.Filters;
using SRT.Complaint.Services;

namespace SRT.Complaint.Controllers.Api;

[ApiController]
[Route("api/stats")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
public class StatsController(
    IStatsService statsService,
    ILogger<StatsController> logger) : ControllerBase
{
    // ─── GET /api/stats/summary ───────────────────────────────────────────────
    [HttpGet("summary")]
    [RequireScope("stats:summary")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        try
        {
            var s = await statsService.GetSummaryAsync(ct);
            return Ok(new
            {
                asOf = s.AsOf,
                complaints = new
                {
                    total       = s.Total,
                    pending     = s.Pending,
                    inProgress  = s.InProgress,
                    resolved    = s.Resolved,
                    closed      = s.Closed,
                    rejected    = s.Rejected,
                    slaBreached = s.SlaBreached,
                    todayNew    = s.TodayNew
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting stats summary");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // ─── GET /api/stats/detailed ─────────────────────────────────────────────
    [HttpGet("detailed")]
    [RequireScope("stats:detailed")]
    public async Task<IActionResult> GetDetailedStats(CancellationToken ct)
    {
        try
        {
            var s = await statsService.GetDetailedAsync(ct);
            return Ok(new
            {
                asOf                   = s.AsOf,
                byCategory             = s.ByCategory,
                byPriority             = s.ByPriority,
                byStatus               = s.ByStatus,
                averageResolutionHours = s.AverageResolutionHours
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting detailed stats");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
