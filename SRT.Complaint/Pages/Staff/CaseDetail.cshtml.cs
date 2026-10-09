#nullable enable
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Models;
using SRT.Complaint.Services;
using SRT.Complaint.Validation;

namespace SRT.Complaint.Pages.Staff;

[Authorize(Policy = "StaffOnly")]
public class CaseDetailModel(
    IComplaintService complaintService,
    IPdfExportService pdfExportService,
    AppDbContext db,
    IConfiguration config) : PageModel
{
    private static readonly string[] AllowedNoteAttachmentExtensions = [".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx"];
    private const long MaxNoteAttachmentSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const int MaxNoteAttachmentCount = 5;

    [BindProperty] public string? NoteContent { get; set; }
    [BindProperty] public string? NoteType { get; set; } = "InternalNote";
    [BindProperty] public List<IFormFile>? NoteAttachments { get; set; }
    [BindProperty] public string? NewStatus { get; set; }
    [BindProperty] public int? TransferToId { get; set; }
    [BindProperty] public string? TransferReason { get; set; }
    [BindProperty] public string? CloseResolution { get; set; }

    public Models.Complaint Complaint { get; private set; } = null!;
    public IEnumerable<SelectListItem> StaffOptions { get; private set; } = [];
    public string[] AllowedNextStatuses { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Response.Headers.CacheControl = "no-store";
        ViewData["Title"] = "รายละเอียดเรื่อง";
        return await LoadComplaintAsync(id);
    }

    public async Task<IActionResult> OnGetAttachmentAsync(int id, int attachmentId)
    {
        var attachment = await db.ComplaintAttachments
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.ComplaintId == id);
        if (attachment == null) return NotFound();

        var storagePath = config["FileUpload:StoragePath"] ?? Path.GetTempPath();
        var fullPath = FileStorage.ResolveWithinRoot(storagePath, attachment.StoredPath);
        if (fullPath is null) return NotFound();
        if (!System.IO.File.Exists(fullPath)) return NotFound();

        return PhysicalFile(fullPath, attachment.MimeType ?? "application/octet-stream", attachment.FileName);
    }

    public async Task<IActionResult> OnPostClaimAsync(int id)
    {
        var complaint = await complaintService.GetByIdAsync(id);
        if (complaint == null) return NotFound();
        // Allow claiming only if unassigned
        if (complaint.AssignedToId != null && !User.IsInRole("SuperAdmin"))
            return Forbid();
        try
        {
            await complaintService.ClaimAsync(id, GetStaffId());
            TempData["Success"] = "รับเรื่องเรียบร้อยแล้ว";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostAddNoteAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        if (string.IsNullOrWhiteSpace(NoteContent))
        {
            TempData["Error"] = "กรุณากรอกเนื้อหาบันทึก";
            return RedirectToPage(new { id });
        }

        var attachments = NoteAttachments?.Where(f => f.Length > 0).ToList() ?? [];
        var (filesValid, fileError) = await ValidateNoteAttachmentsAsync(attachments);
        if (!filesValid)
        {
            TempData["Error"] = fileError;
            return RedirectToPage(new { id });
        }

        await complaintService.AddNoteAsync(id, GetStaffId(), NoteType ?? "InternalNote", NoteContent.Trim(), attachments);
        TempData["Success"] = "บันทึกสำเร็จ";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUpdateStatusAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        if (string.IsNullOrEmpty(NewStatus))
        {
            TempData["Error"] = "กรุณาเลือกสถานะ";
            return RedirectToPage(new { id });
        }
        await complaintService.UpdateStatusAsync(id, NewStatus, GetStaffId(), null);
        TempData["Success"] = $"อัปเดตสถานะเป็น \"{StatusLabel(NewStatus)}\" เรียบร้อย";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostTransferAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        if (!TransferToId.HasValue || string.IsNullOrWhiteSpace(TransferReason))
        {
            TempData["Error"] = "กรุณาเลือกผู้รับโอนและระบุเหตุผล";
            return RedirectToPage(new { id });
        }
        await complaintService.TransferAsync(id, TransferToId.Value, TransferReason.Trim(), GetStaffId());
        TempData["Success"] = "โอนเรื่องเรียบร้อยแล้ว";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCloseAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        if (string.IsNullOrWhiteSpace(CloseResolution))
        {
            TempData["Error"] = "กรุณากรอกสรุปการดำเนินการก่อนปิดเรื่อง";
            return RedirectToPage(new { id });
        }
        try
        {
            await complaintService.CloseAsync(id, CloseResolution.Trim(), GetStaffId());
            TempData["Success"] = "ปิดเรื่องร้องเรียนเรียบร้อยแล้ว";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"เกิดข้อผิดพลาด: {ex.Message}";
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReopenAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        await complaintService.ReopenAsync(id, GetStaffId());
        TempData["Success"] = "เปิดเรื่องใหม่เรียบร้อย กำลังดำเนินการอีกครั้ง";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDownloadPdfAsync(int id, [FromForm] bool maskReporter = false)
    {
        var complaint = await complaintService.GetByIdAsync(id);
        if (complaint == null) return NotFound();

        var officerName = User.FindFirstValue("FullName") ?? User.FindFirstValue(ClaimTypes.Name);
        var pdf = pdfExportService.GenerateComplaintPdf(complaint, officerName, maskReporter);
        var suffix = maskReporter ? "-masked" : "";
        return File(pdf, "application/pdf", $"complaint-{complaint.ReferenceNumber}{suffix}.pdf");
    }

    private async Task<IActionResult> LoadComplaintAsync(int id)
    {
        var complaint = await complaintService.GetByIdAsync(id);
        if (complaint == null) return NotFound();
        Complaint = complaint;
        ViewData["Title"] = $"เรื่อง {complaint.ReferenceNumber}";

        AllowedNextStatuses = complaint.Status switch
        {
            "Pending"    => ["InProgress"],
            "InProgress" => ["Resolved", "Rejected"],
            _            => []
        };

        StaffOptions = await db.StaffUsers
            .Where(u => u.IsActive && (u.Role == "GeneralOfficer" || u.Role == "SuperAdmin")
                     && u.Id != complaint.AssignedToId)
            .OrderBy(u => u.FullName)
            .Select(u => new SelectListItem($"{u.FullName} ({u.EmployeeCode})", u.Id.ToString()))
            .ToListAsync();

        return Page();
    }

    private int GetStaffId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<bool> CanModifyAsync(int complaintId)
    {
        if (User.IsInRole("SuperAdmin")) return true;
        var c = await complaintService.GetByIdAsync(complaintId);
        return c?.AssignedToId == GetStaffId();
    }

    private static async Task<(bool Valid, string Error)> ValidateNoteAttachmentsAsync(IReadOnlyList<IFormFile> files)
    {
        if (files.Count == 0) return (true, string.Empty);
        if (files.Count > MaxNoteAttachmentCount)
            return (false, $"แนบไฟล์ได้ไม่เกิน {MaxNoteAttachmentCount} ไฟล์");

        foreach (var file in files)
        {
            if (file.Length > MaxNoteAttachmentSizeBytes)
                return (false, $"ไฟล์ \"{file.FileName}\" มีขนาดเกิน 10 MB");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedNoteAttachmentExtensions.Contains(ext))
                return (false, $"ไฟล์ \"{file.FileName}\" ไม่รองรับประเภทไฟล์นี้ (รองรับ: JPG, PNG, PDF, DOC, DOCX)");

            if (!await FileSignatureValidator.HasValidSignatureAsync(file, ext))
                return (false, $"ไฟล์ \"{file.FileName}\" มีเนื้อหาไม่ตรงกับนามสกุลไฟล์");
        }
        return (true, string.Empty);
    }

    public static string StatusLabel(string s) => s switch
    {
        "Pending"    => "รอรับเรื่อง",
        "InProgress" => "กำลังดำเนินการ",
        "Resolved"   => "แก้ไขแล้ว",
        "Closed"     => "ปิดเรื่อง",
        "Rejected"   => "ปฏิเสธ",
        _            => s
    };

    public static string PriorityLabel(string p) => p switch
    {
        "Critical" => "เร่งด่วนมาก",
        "Urgent"   => "เร่งด่วน",
        "High"     => "สำคัญ",
        "Normal"   => "ปกติ",
        "Low"      => "ข้อเสนอแนะ",
        _          => p
    };
}
