#nullable enable
using Microsoft.AspNetCore.Mvc;
using SRT.Complaint.Filters;
using SRT.Complaint.Services;

namespace SRT.Complaint.Controllers.Api;

[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
[Route("api/v1/stats/corruption")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
public class CorruptionStatsController(
    ICorruptionStatsService corruptionStatsService,
    ILogger<CorruptionStatsController> logger) : ControllerBase
{
    // GET /api/v1/stats/corruption
    [HttpGet]
    [RequireScope("corruption:stats")]
    [RequireDataAccess("All")]
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
            return StatusCode(500, new
            {
                error = new
                {
                    code = "internal_error",
                    message = "Internal server error",
                    traceId = HttpContext.TraceIdentifier
                }
            });
        }
    }
}
