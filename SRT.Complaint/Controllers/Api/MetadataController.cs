using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Filters;
using SRT.Complaint.Services;

namespace SRT.Complaint.Controllers.Api;

[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
[Route("api/v1/metadata")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
[RequireScope("metadata:read")]
[Produces("application/json")]
public class MetadataController(AppDbContext db) : ControllerBase
{
    [HttpGet("categories", Name = "GetCategoriesV1")]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var categories = await db.ComplaintCategories.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.DepartmentName,
                subCategories = x.SubCategories.Where(s => s.IsActive).OrderBy(s => s.SortOrder)
                    .Select(s => new { s.Id, s.Name })
            })
            .ToListAsync(ct);
        return Ok(new { data = categories });
    }

    [HttpGet("statuses", Name = "GetStatusesV1")]
    public IActionResult GetStatuses()
    {
        var statuses = ApiIntegrationPolicy.AllowedComplaintStatuses
            .Select(x => new { code = x, labelTh = ComplaintsController.StatusLabel(x) })
            .OrderBy(x => x.code)
            .ToList();
        return Ok(new { data = statuses });
    }

    [HttpGet("webhook-events", Name = "GetWebhookEventsV1")]
    public IActionResult GetWebhookEvents() => Ok(new
    {
        data = new[]
        {
            new { code = "complaint.created", description = "สร้างเรื่องร้องเรียนแล้ว" },
            new { code = "complaint.status_changed", description = "สถานะเรื่องร้องเรียนเปลี่ยนแปลง" },
            new { code = "complaint.closed", description = "เรื่องร้องเรียนถูกแก้ไขหรือปิดแล้ว" }
        }
    });
}
