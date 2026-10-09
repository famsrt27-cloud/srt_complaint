#nullable enable
using Microsoft.AspNetCore.Mvc;
using SRT.Complaint.Filters;
using SRT.Complaint.Models;
using SRT.Complaint.Services;

namespace SRT.Complaint.Controllers.Api;

[ApiController]
[Route("api/complaints")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
public class ComplaintsController(
    IComplaintService complaintService,
    ILogger<ComplaintsController> logger) : ControllerBase
{
    // ─── GET /api/complaints/{referenceNumber} ───────────────────────────────
    [HttpGet("{referenceNumber}")]
    [RequireScope("complaints:read")]
    public async Task<IActionResult> GetComplaint(string referenceNumber, CancellationToken ct)
    {
        try
        {
            var complaint = await complaintService.GetByReferenceAsync(referenceNumber, ct);
            if (complaint == null)
                return NotFound(new { error = $"Complaint '{referenceNumber}' not found" });

            return Ok(new ComplaintReadDto(
                complaint.ReferenceNumber,
                complaint.Status,
                StatusLabel(complaint.Status),
                complaint.Priority,
                PriorityLabel(complaint.Priority),
                complaint.Category?.Name ?? "",
                complaint.Category?.DepartmentName,
                complaint.ReporterName,
                complaint.ReporterPhone,
                complaint.ReporterEmail,
                complaint.SubjectStation,
                complaint.IncidentDate?.ToString("yyyy-MM-dd"),
                complaint.Description,
                complaint.AssignedTo?.FullName,
                complaint.SlaDeadline,
                complaint.SlaBreached,
                complaint.CreatedAt,
                complaint.UpdatedAt,
                complaint.ClosedAt,
                complaint.SatisfactionScore
            ));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting complaint {Ref}", referenceNumber);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // ─── POST /api/complaints ─────────────────────────────────────────────────
    [HttpPost]
    [RequireScope("complaints:write")]
    public async Task<IActionResult> CreateComplaint([FromBody] ComplaintCreateDto dto, CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new { error = "Invalid request", details = ModelState });

            var request = new SubmitComplaintRequest(
                ReporterName:   dto.ReporterName,
                ReporterPhone:  dto.ReporterPhone,
                ReporterEmail:  dto.ReporterEmail,
                ReporterIdCard: null,
                CategoryId:     dto.CategoryId,
                SubCategoryId:  null,
                SubjectStation: dto.SubjectStation,
                IncidentDate:   dto.IncidentDate.HasValue ? DateOnly.FromDateTime(dto.IncidentDate.Value) : null,
                Description:    dto.Description,
                Attachments:    Array.Empty<IFormFile>()
            );

            var complaint = await complaintService.SubmitAsync(request, ct);
            return StatusCode(201, new
            {
                referenceNumber = complaint.ReferenceNumber,
                status          = complaint.Status,
                slaDeadline     = complaint.SlaDeadline,
                trackingUrl     = $"https://www.railway.co.th/complaint/track/{complaint.ReferenceNumber}",
                message         = "รับเรื่องร้องเรียนเรียบร้อยแล้ว"
            });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Validation error creating complaint");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating complaint");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // ─── GET /api/complaints/{referenceNumber}/status ────────────────────────
    [HttpGet("{referenceNumber}/status")]
    [RequireScope("complaints:status")]
    public async Task<IActionResult> GetComplaintStatus(string referenceNumber, CancellationToken ct)
    {
        try
        {
            var complaint = await complaintService.GetByReferenceAsync(referenceNumber, ct);
            if (complaint == null)
                return NotFound(new { error = $"Complaint '{referenceNumber}' not found" });

            return Ok(new
            {
                referenceNumber = complaint.ReferenceNumber,
                status          = complaint.Status,
                statusTh        = StatusLabel(complaint.Status),
                updatedAt       = complaint.UpdatedAt,
                closedAt        = complaint.ClosedAt,
                slaBreached     = complaint.SlaBreached
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting status for {Ref}", referenceNumber);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // ─── PUT /api/complaints/{referenceNumber}/status ─────────────────────────
    [HttpPut("{referenceNumber}/status")]
    [RequireScope("complaints:update")]
    public async Task<IActionResult> UpdateComplaintStatus(
        string referenceNumber, [FromBody] UpdateStatusDto dto, CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new { error = "Invalid request", details = ModelState });

            var complaint = await complaintService.GetByReferenceAsync(referenceNumber, ct);
            if (complaint == null)
                return NotFound(new { error = $"Complaint '{referenceNumber}' not found" });

            var apiKey = (HttpContext.Items["ApiKey"] as ApiKey)!;
            await complaintService.UpdateStatusAsync(complaint.Id, dto.NewStatus, apiKey.CreatedById, dto.Note, ct);
            return Ok(new { referenceNumber, newStatus = dto.NewStatus, message = "อัปเดตสถานะเรียบร้อยแล้ว" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating status for {Ref}", referenceNumber);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // ─── GET /api/complaints/{referenceNumber}/edoc-payload ──────────────────
    [HttpGet("{referenceNumber}/edoc-payload")]
    [RequireScope("complaints:edoc")]
    public async Task<IActionResult> GetEdocPayload(string referenceNumber, CancellationToken ct)
    {
        try
        {
            var complaint = await complaintService.GetByReferenceAsync(referenceNumber, ct);
            if (complaint == null)
                return NotFound(new { error = $"Complaint '{referenceNumber}' not found" });

            var lastResolutionNote = complaint.Notes
                .Where(n => n.NoteType is "PublicReply" or "Internal")
                .OrderByDescending(n => n.CreatedAt)
                .FirstOrDefault()?.Content;

            var payload = new EdocPayloadDto(
                SchemaVersion:  "1.0",
                GeneratedAt:    DateTime.UtcNow,
                ReferenceNumber: complaint.ReferenceNumber,
                Reporter: new EdocReporterDto(
                    complaint.ReporterName,
                    complaint.ReporterPhone,
                    complaint.ReporterEmail),
                Complaint: new EdocComplaintDto(
                    complaint.Category?.Name ?? "",
                    complaint.Category?.DepartmentName,
                    complaint.Priority,
                    PriorityLabel(complaint.Priority),
                    complaint.SubjectStation,
                    complaint.IncidentDate?.ToString("yyyy-MM-dd"),
                    complaint.Description,
                    complaint.CreatedAt),
                Resolution: new EdocResolutionDto(
                    complaint.Status,
                    StatusLabel(complaint.Status),
                    complaint.AssignedTo?.FullName,
                    complaint.SlaDeadline,
                    complaint.SlaBreached,
                    complaint.ClosedAt,
                    lastResolutionNote)
            );

            return Ok(payload);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting eDOC payload for {Ref}", referenceNumber);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private static string StatusLabel(string s) => s switch
    {
        "Pending"     => "รอดำเนินการ",
        "InProgress"  => "กำลังดำเนินการ",
        "WaitingInfo" => "รอข้อมูลเพิ่มเติม",
        "Forwarded"   => "ส่งต่อแผนก",
        "Resolved"    => "แก้ไขแล้ว",
        "Closed"      => "ปิดเรื่อง",
        "Rejected"    => "ปฏิเสธ",
        _             => s
    };

    private static string PriorityLabel(string p) => p switch
    {
        "Critical" => "เร่งด่วนมาก",
        "Urgent"   => "เร่งด่วน",
        "High"     => "สำคัญ",
        "Normal"   => "ปกติ",
        "Low"      => "ข้อเสนอแนะ",
        _          => p
    };
}

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public record ComplaintReadDto(
    string     ReferenceNumber,
    string     Status,
    string     StatusTh,
    string     Priority,
    string     PriorityTh,
    string     Category,
    string?    Department,
    string     ReporterName,
    string     ReporterPhone,
    string?    ReporterEmail,
    string?    SubjectStation,
    string?    IncidentDate,
    string     Description,
    string?    AssignedTo,
    DateTime?  SlaDeadline,
    bool       SlaBreached,
    DateTime   CreatedAt,
    DateTime   UpdatedAt,
    DateTime?  ClosedAt,
    byte?      SatisfactionScore
);

public record ComplaintCreateDto(
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.MaxLength(200)]
    string ReporterName,

    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.MaxLength(20)]
    string ReporterPhone,

    [property: System.ComponentModel.DataAnnotations.EmailAddress]
    string? ReporterEmail,

    [property: System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
    int CategoryId,

    string? SubjectStation,
    DateTime? IncidentDate,

    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.MinLength(10)]
    string Description
);

public record EdocPayloadDto(
    string           SchemaVersion,
    DateTime         GeneratedAt,
    string           ReferenceNumber,
    EdocReporterDto  Reporter,
    EdocComplaintDto Complaint,
    EdocResolutionDto Resolution
);

public record EdocReporterDto(string Name, string Phone, string? Email);

public record EdocComplaintDto(
    string   Category,
    string?  Department,
    string   Priority,
    string   PriorityTh,
    string?  SubjectStation,
    string?  IncidentDate,
    string   Description,
    DateTime SubmittedAt
);

public record UpdateStatusDto(
    [property: System.ComponentModel.DataAnnotations.Required]
    string NewStatus,
    string? Note
);

public record EdocResolutionDto(
    string    Status,
    string    StatusTh,
    string?   AssignedTo,
    DateTime? SlaDeadline,
    bool      SlaBreached,
    DateTime? ClosedAt,
    string?   ResolutionNote
);
