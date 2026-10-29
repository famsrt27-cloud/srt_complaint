#nullable enable
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Filters;
using SRT.Complaint.Models;
using SRT.Complaint.Services;

namespace SRT.Complaint.Controllers.Api;

[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
[Route("api/v1/complaints")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
[Produces("application/json")]
public class ComplaintsController(
    AppDbContext db,
    IComplaintService complaintService,
    IIdempotencyService idempotencyService,
    ILogger<ComplaintsController> logger) : ControllerBase
{
    [HttpGet(Name = "ListComplaintsV1")]
    [RequireScope("complaints:read")]
    public async Task<IActionResult> ListComplaints(
        [FromQuery] DateTime? updatedSince,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
            return BadRequest(ApiError("invalid_pagination", "page must be >= 1 and pageSize must be between 1 and 100"));
        if (!string.IsNullOrWhiteSpace(status) && !ApiIntegrationPolicy.AllowedComplaintStatuses.Contains(status))
            return BadRequest(ApiError("invalid_status", "Unknown complaint status"));

        var key = CurrentApiKey;
        var query = db.Complaints.AsNoTracking().Include(x => x.Category).AsQueryable();
        if (key.DataAccessMode != "All") query = query.Where(x => x.SourcePartnerId == key.PartnerId);
        if (updatedSince.HasValue) query = query.Where(x => x.UpdatedAt >= updatedSince.Value.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.UpdatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.ReferenceNumber,
                externalReference = x.ExternalId,
                x.Status,
                x.Priority,
                category = x.Category.Name,
                x.SubjectStation,
                x.CreatedAt,
                x.UpdatedAt
            }).ToListAsync(ct);

        return Ok(new { data = items, pagination = new { page, pageSize, total, totalPages = (int)Math.Ceiling((double)total / pageSize) } });
    }

    [HttpGet("by-external-reference/{externalReference}", Name = "GetComplaintByExternalReferenceV1")]
    [RequireScope("complaints:read")]
    public async Task<IActionResult> GetByExternalReference(string externalReference, CancellationToken ct)
    {
        var key = CurrentApiKey;
        var query = db.Complaints.AsNoTracking()
            .Include(x => x.Category).Include(x => x.AssignedTo).Include(x => x.Notes)
            .Where(x => x.ExternalId == externalReference);
        if (key.DataAccessMode != "All") query = query.Where(x => x.SourcePartnerId == key.PartnerId);
        var complaint = await query.FirstOrDefaultAsync(ct);
        return complaint == null
            ? NotFound(ApiError("not_found", "Complaint not found"))
            : Ok(ToReadDto(complaint));
    }

    [HttpGet("{referenceNumber}", Name = "GetComplaintV1")]
    [RequireScope("complaints:read")]
    [ProducesResponseType<ComplaintReadDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetComplaint(string referenceNumber, CancellationToken ct)
    {
        try
        {
            var complaint = await complaintService.GetByReferenceAsync(referenceNumber, ct);
            if (complaint == null || !CanAccess(complaint, CurrentApiKey))
                return NotFound(ApiError("not_found", "Complaint not found"));

            return Ok(ToReadDto(complaint));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting complaint {ReferenceNumber}", referenceNumber);
            return StatusCode(500, ApiError("internal_error", "Internal server error"));
        }
    }

    [HttpPost(Name = "CreateComplaintV1")]
    [RequireScope("complaints:write")]
    [RequestSizeLimit(256 * 1024)]
    [ProducesResponseType<ComplaintCreatedDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateComplaint(
        [FromBody] ComplaintCreateDto dto,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var normalizedIdempotencyKey = idempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedIdempotencyKey) || normalizedIdempotencyKey.Length is < 8 or > 128)
            return BadRequest(ApiError("invalid_idempotency_key", "Idempotency-Key header is required and must contain 8-128 characters"));

        var apiKey = CurrentApiKey;
        var requestHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dto)))).ToLowerInvariant();
        var begin = await idempotencyService.TryBeginAsync(apiKey.Id, normalizedIdempotencyKey, requestHash, ct);

        if (begin.State == IdempotencyBeginState.PayloadConflict)
            return Conflict(ApiError("idempotency_conflict", "This Idempotency-Key was already used with a different request"));
        if (begin.State == IdempotencyBeginState.InProgress)
            return StatusCode(StatusCodes.Status409Conflict,
                ApiError("request_in_progress", "A request with this Idempotency-Key is still being processed"));
        if (begin.State == IdempotencyBeginState.Replay)
        {
            Response.Headers["Idempotent-Replayed"] = "true";
            return new ContentResult
            {
                Content = begin.ResponseBody ?? "{}",
                ContentType = "application/json; charset=utf-8",
                StatusCode = begin.ResponseStatus ?? StatusCodes.Status200OK
            };
        }

        try
        {
            var externalReference = dto.ExternalReference!.Trim();
            if (await db.Complaints.AsNoTracking().AnyAsync(
                    x => x.SourcePartnerId == apiKey.PartnerId && x.ExternalId == externalReference, ct))
            {
                await idempotencyService.AbandonAsync(begin.RecordId, ct);
                return Conflict(ApiError("duplicate_external_reference", "externalReference already exists for this partner"));
            }

            var request = new SubmitComplaintRequest(
                ReporterName: dto.ReporterName.Trim(),
                ReporterPhone: dto.ReporterPhone.Trim(),
                ReporterEmail: string.IsNullOrWhiteSpace(dto.ReporterEmail) ? null : dto.ReporterEmail.Trim(),
                ReporterIdCard: null,
                CategoryId: dto.CategoryId,
                SubCategoryId: dto.SubCategoryId,
                SubjectStation: string.IsNullOrWhiteSpace(dto.SubjectStation) ? null : dto.SubjectStation.Trim(),
                IncidentDate: dto.IncidentDate.HasValue ? DateOnly.FromDateTime(dto.IncidentDate.Value) : null,
                Description: dto.Description.Trim(),
                Attachments: Array.Empty<IFormFile>(),
                Channel: "API",
                Latitude: dto.Latitude,
                Longitude: dto.Longitude,
                SourcePartnerId: apiKey.PartnerId,
                ExternalSystem: apiKey.Partner.Code,
                ExternalId: externalReference);

            var complaint = await complaintService.SubmitAsync(request, ct);
            var response = new ComplaintCreatedDto(
                complaint.ReferenceNumber,
                dto.ExternalReference,
                complaint.Status,
                complaint.SlaDeadline,
                Url.RouteUrl("GetComplaintV1", new { referenceNumber = complaint.ReferenceNumber }),
                "รับเรื่องร้องเรียนเรียบร้อยแล้ว");
            var responseBody = JsonSerializer.Serialize(response, ApiJsonOptions);
            await idempotencyService.CompleteAsync(begin.RecordId, StatusCodes.Status201Created, responseBody, ct);

            return new ContentResult
            {
                Content = responseBody,
                ContentType = "application/json; charset=utf-8",
                StatusCode = StatusCodes.Status201Created
            };
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            await idempotencyService.AbandonAsync(begin.RecordId, ct);
            logger.LogWarning(ex, "Duplicate external reference for partner {PartnerId}", apiKey.PartnerId);
            return Conflict(ApiError("duplicate_external_reference", "externalReference already exists for this partner"));
        }
        catch (InvalidOperationException ex)
        {
            await idempotencyService.AbandonAsync(begin.RecordId, ct);
            logger.LogWarning(ex, "Validation error creating complaint");
            return UnprocessableEntity(ApiError("validation_error", ex.Message));
        }
        catch (Exception ex)
        {
            await idempotencyService.AbandonAsync(begin.RecordId, ct);
            logger.LogError(ex, "Error creating complaint");
            return StatusCode(500, ApiError("internal_error", "Internal server error"));
        }
    }

    [HttpGet("{referenceNumber}/status", Name = "GetComplaintStatusV1")]
    [RequireScope("complaints:status")]
    public async Task<IActionResult> GetComplaintStatus(string referenceNumber, CancellationToken ct)
    {
        var complaint = await complaintService.GetByReferenceAsync(referenceNumber, ct);
        if (complaint == null || !CanAccess(complaint, CurrentApiKey))
            return NotFound(ApiError("not_found", "Complaint not found"));

        return Ok(new
        {
            referenceNumber = complaint.ReferenceNumber,
            externalReference = complaint.ExternalId,
            status = complaint.Status,
            statusTh = StatusLabel(complaint.Status),
            updatedAt = complaint.UpdatedAt,
            closedAt = complaint.ClosedAt,
            slaBreached = complaint.SlaBreached
        });
    }

    [HttpPut("{referenceNumber}/status", Name = "UpdateComplaintStatusV1")]
    [RequireScope("complaints:update")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> UpdateComplaintStatus(
        string referenceNumber, [FromBody] UpdateStatusDto dto, CancellationToken ct)
    {
        if (!ApiIntegrationPolicy.AllowedComplaintStatuses.Contains(dto.NewStatus))
            return UnprocessableEntity(ApiError("invalid_status", "Unknown complaint status"));

        var complaint = await complaintService.GetByReferenceAsync(referenceNumber, ct);
        if (complaint == null || !CanAccess(complaint, CurrentApiKey))
            return NotFound(ApiError("not_found", "Complaint not found"));

        await complaintService.UpdateStatusAsync(complaint.Id, dto.NewStatus, CurrentApiKey.CreatedById, dto.Note, ct);
        return Ok(new { referenceNumber, newStatus = dto.NewStatus, message = "อัปเดตสถานะเรียบร้อยแล้ว" });
    }

    [HttpGet("{referenceNumber}/edoc-payload", Name = "GetComplaintEdocPayloadV1")]
    [RequireScope("complaints:edoc")]
    public async Task<IActionResult> GetEdocPayload(string referenceNumber, CancellationToken ct)
    {
        var complaint = await complaintService.GetByReferenceAsync(referenceNumber, ct);
        if (complaint == null || !CanAccess(complaint, CurrentApiKey))
            return NotFound(ApiError("not_found", "Complaint not found"));

        var lastResolutionNote = complaint.Notes
            .Where(n => n.NoteType is "PublicReply" or "Internal")
            .OrderByDescending(n => n.CreatedAt)
            .FirstOrDefault()?.Content;

        return Ok(new EdocPayloadDto(
            "1.0", DateTime.UtcNow, complaint.ReferenceNumber,
            new EdocReporterDto(complaint.ReporterName, complaint.ReporterPhone, complaint.ReporterEmail),
            new EdocComplaintDto(
                complaint.Category?.Name ?? "", complaint.Category?.DepartmentName, complaint.Priority,
                PriorityLabel(complaint.Priority), complaint.SubjectStation,
                complaint.IncidentDate?.ToString("yyyy-MM-dd"), complaint.Description, complaint.CreatedAt),
            new EdocResolutionDto(
                complaint.Status, StatusLabel(complaint.Status), complaint.AssignedTo?.FullName,
                complaint.SlaDeadline, complaint.SlaBreached, complaint.ClosedAt, lastResolutionNote)));
    }

    private ApiKey CurrentApiKey => (HttpContext.Items["ApiKey"] as ApiKey)!;

    private static bool CanAccess(Models.Complaint complaint, ApiKey key) =>
        key.DataAccessMode == "All" || complaint.SourcePartnerId == key.PartnerId;

    private static object ApiError(string code, string message) => new { error = new { code, message } };

    private static ComplaintReadDto ToReadDto(Models.Complaint complaint) => new(
        complaint.ReferenceNumber, complaint.ExternalId, complaint.Status, StatusLabel(complaint.Status),
        complaint.Priority, PriorityLabel(complaint.Priority), complaint.Category?.Name ?? "",
        complaint.Category?.DepartmentName, complaint.ReporterName, complaint.ReporterPhone,
        complaint.ReporterEmail, complaint.SubjectStation, complaint.IncidentDate?.ToString("yyyy-MM-dd"),
        complaint.Description, complaint.AssignedTo?.FullName, complaint.SlaDeadline, complaint.SlaBreached,
        complaint.CreatedAt, complaint.UpdatedAt, complaint.ClosedAt, complaint.SatisfactionScore);

    private static readonly JsonSerializerOptions ApiJsonOptions = new(JsonSerializerDefaults.Web);

    internal static string StatusLabel(string status) => status switch
    {
        "Pending" => "รอดำเนินการ",
        "InProgress" => "กำลังดำเนินการ",
        "WaitingInfo" => "รอข้อมูลเพิ่มเติม",
        "Forwarded" => "ส่งต่อหน่วยงาน",
        "Resolved" => "แก้ไขแล้ว",
        "Closed" => "ปิดเรื่อง",
        "Rejected" => "ปฏิเสธ",
        _ => status
    };

    internal static string PriorityLabel(string priority) => priority switch
    {
        "Critical" => "เร่งด่วนมาก",
        "Urgent" => "เร่งด่วน",
        "High" => "สำคัญ",
        "Normal" => "ปกติ",
        "Low" => "ข้อเสนอแนะ",
        _ => priority
    };
}

public record ComplaintReadDto(
    string ReferenceNumber,
    string? ExternalReference,
    string Status,
    string StatusTh,
    string Priority,
    string PriorityTh,
    string Category,
    string? Department,
    string ReporterName,
    string ReporterPhone,
    string? ReporterEmail,
    string? SubjectStation,
    string? IncidentDate,
    string Description,
    string? AssignedTo,
    DateTime? SlaDeadline,
    bool SlaBreached,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ClosedAt,
    byte? SatisfactionScore);

public record ComplaintCreateDto(
    [property: Required, MaxLength(100)] string? ExternalReference,
    [property: Required, MaxLength(200)] string ReporterName,
    [property: Required, MaxLength(20)] string ReporterPhone,
    [property: EmailAddress, MaxLength(200)] string? ReporterEmail,
    [property: Range(1, int.MaxValue)] int CategoryId,
    int? SubCategoryId,
    [property: MaxLength(200)] string? SubjectStation,
    DateTime? IncidentDate,
    [property: Required, MinLength(10), MaxLength(5000)] string Description,
    [property: Range(-90, 90)] double? Latitude,
    [property: Range(-180, 180)] double? Longitude);

public record ComplaintCreatedDto(
    string ReferenceNumber,
    string? ExternalReference,
    string Status,
    DateTime? SlaDeadline,
    string? ResourceUrl,
    string Message);

public record UpdateStatusDto(
    [property: Required] string NewStatus,
    [property: MaxLength(2000)] string? Note);

public record EdocPayloadDto(
    string SchemaVersion,
    DateTime GeneratedAt,
    string ReferenceNumber,
    EdocReporterDto Reporter,
    EdocComplaintDto Complaint,
    EdocResolutionDto Resolution);

public record EdocReporterDto(string Name, string Phone, string? Email);

public record EdocComplaintDto(
    string Category,
    string? Department,
    string Priority,
    string PriorityTh,
    string? SubjectStation,
    string? IncidentDate,
    string Description,
    DateTime SubmittedAt);

public record EdocResolutionDto(
    string Status,
    string StatusTh,
    string? AssignedTo,
    DateTime? SlaDeadline,
    bool SlaBreached,
    DateTime? ClosedAt,
    string? ResolutionNote);
