#nullable enable
using Microsoft.AspNetCore.Mvc;
using SRT.Complaint.Filters;
using SRT.Complaint.Services;

namespace SRT.Complaint.Controllers.Api;

[ApiController]
[Route("api/stats/corruption")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
public class CorruptionStatsController(
    ICorruptionStatsService corruptionStatsService,
    ILogger<CorruptionStatsController> logger) : ControllerBase
{
    // ─── GET /api/stats/corruption ────────────────────────────────────────────
    [HttpGet]
    [RequireScope("corruption:stats")]
    public async Task<IActionResult> GetCorruptionStats(CancellationToken ct)
    {
        try
        {
            var s = await corruptionStatsService.GetSummaryAsync(ct);
            return Ok(new
            {
                asOf = s.AsOf,
                reports = new
                {
                    total       = s.Total,
                    pending     = s.Pending,
                    inProgress  = s.InProgress,
                    underReview = s.UnderReview,
                    closed      = s.Closed,
                    rejected    = s.Rejected,
                    slaBreached = s.SlaBreached,
                    todayNew    = s.TodayNew
                },
                bySubjectType = s.BySubjectType
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting corruption stats");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
