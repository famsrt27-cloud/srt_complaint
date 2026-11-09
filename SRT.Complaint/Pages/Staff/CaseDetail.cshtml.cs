#nullable enable
using System.Security.Claims;
using System.Text.Json;
using System.Net;
using System.Text;
using ClosedXML.Excel;
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

[Authorize(Policy = "GeneralComplaintAccess")]
public class CaseDetailModel(
    IComplaintService complaintService,
    IPdfExportService pdfExportService,
    IPdfSignerSettingsService pdfSignerSettings,
    IEdocIntegrationService edocIntegrationService,
    AppDbContext db,
    IConfiguration config,
    IAuditService auditService) : PageModel
{
    private static readonly string[] AllowedNoteAttachmentExtensions = [".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx"];
    public static readonly IReadOnlyDictionary<string, string> CloseOpeningOptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Investigated"] = "การรถไฟแห่งประเทศไทยได้ดำเนินการตรวจสอบเรื่องดังกล่าวแล้ว พบว่า",
            ["Completed"] = "หน่วยงานที่เกี่ยวข้องได้ดำเนินการแก้ไขเรียบร้อยแล้ว โดย",
            ["Clarified"] = "การรถไฟแห่งประเทศไทยขอชี้แจงผลการดำเนินการว่า"
        };
    private const long MaxNoteAttachmentSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const int MaxNoteAttachmentCount = 5;

    [BindProperty] public string? NoteContent { get; set; }
    [BindProperty] public string? NoteType { get; set; } = "InternalNote";
    [BindProperty] public List<IFormFile>? NoteAttachments { get; set; }
    [BindProperty] public int? TransferToId { get; set; }
    [BindProperty] public string? TransferReason { get; set; }
    [BindProperty] public string? CloseOpening { get; set; }
    [BindProperty] public string? CloseResolution { get; set; }
    [BindProperty] public string? EdocBasketId { get; set; }
    [BindProperty] public string? PdfDepartmentName { get; set; }
    [BindProperty] public string? PdfDepartmentOther { get; set; }
    [BindProperty] public int ClassificationCategoryId { get; set; }
    [BindProperty] public int? ClassificationSubCategoryId { get; set; }
    [BindProperty] public string? ClassificationDepartment { get; set; }
    [BindProperty] public string? WorkflowReason { get; set; }
    [BindProperty] public string? InvestigationSummary { get; set; }
    [BindProperty] public string? InvestigationNote { get; set; }
    [BindProperty] public List<IFormFile>? InvestigationAttachments { get; set; }
    [BindProperty] public string? NoEvidenceReason { get; set; }
    [BindProperty] public bool ClosureChecklistConfirmed { get; set; }

    public Models.Complaint Complaint { get; private set; } = null!;
    public IEnumerable<SelectListItem> StaffOptions { get; private set; } = [];
    public EdocDocumentLink? EdocLink { get; private set; }
    public IReadOnlyList<EdocBasketOption> EdocBaskets { get; private set; } = [];
    public IReadOnlyList<string> PdfDepartmentOptions { get; private set; } = [];
    public IEnumerable<SelectListItem> ClassificationCategoryOptions { get; private set; } = [];
    public IReadOnlyList<string> ClassificationDepartmentOptions { get; private set; } = [];
    public string ClassificationSubCategoriesJson { get; private set; } = "{}";
    public string ClassificationDepartmentsByCategoryJson { get; private set; } = "{}";
    public string? DefaultEdocBasketId { get; private set; }
    public bool EdocEnabled => edocIntegrationService.IsEnabled;
    public int CurrentWorkflowNumber => ComplaintWorkflowSteps.Number(Complaint.WorkflowStep);

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

    public async Task<IActionResult> OnPostCompleteScreeningAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        try
        {
            await complaintService.CompleteScreeningAsync(id, true, null, GetStaffId());
            TempData["Success"] = "ยืนยันข้อมูลครบถ้วนแล้ว ไปยังขั้นจำแนกเรื่อง";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRequestMoreInfoAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        try
        {
            await complaintService.CompleteScreeningAsync(id, false, WorkflowReason, GetStaffId());
            TempData["Success"] = "บันทึกการขอข้อมูลเพิ่มเติมแล้ว";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostResumeScreeningAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        try
        {
            await complaintService.ResumeScreeningAsync(id, GetStaffId());
            TempData["Success"] = "บันทึกว่าได้รับข้อมูลเพิ่มเติมแล้ว";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCompleteClassificationAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        try
        {
            await complaintService.CompleteClassificationAsync(id, GetStaffId());
            TempData["Success"] = "จำแนกเรื่องครบถ้วนแล้ว เริ่มดำเนินการได้";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSaveAndCompleteClassificationAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        if (ClassificationCategoryId <= 0)
        {
            TempData["Error"] = "กรุณาเลือกประเภทเรื่อง";
            return RedirectToPage(new { id });
        }

        try
        {
            await complaintService.UpdateClassificationAsync(
                id,
                ClassificationCategoryId,
                ClassificationSubCategoryId > 0 ? ClassificationSubCategoryId : null,
                ClassificationDepartment,
                GetStaffId());
            await complaintService.CompleteClassificationAsync(id, GetStaffId());
            TempData["Success"] = "บันทึกการจำแนกและเริ่มขั้นดำเนินการแล้ว";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCompleteInvestigationAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        if (string.IsNullOrWhiteSpace(InvestigationSummary) || InvestigationSummary.Trim().Length < 20)
        {
            TempData["Error"] = "กรุณาสรุปผลการตรวจสอบอย่างน้อย 20 ตัวอักษร";
            return RedirectToPage(new { id });
        }
        if (string.IsNullOrWhiteSpace(InvestigationNote))
        {
            TempData["Error"] = "กรุณาบันทึกสิ่งที่ดำเนินการอย่างน้อยหนึ่งรายการ";
            return RedirectToPage(new { id });
        }

        var attachments = InvestigationAttachments?.Where(f => f.Length > 0).ToList() ?? [];
        var (filesValid, fileError) = await ValidateNoteAttachmentsAsync(attachments);
        if (!filesValid)
        {
            TempData["Error"] = fileError;
            return RedirectToPage(new { id });
        }
        var alreadyHasEvidence = await db.ComplaintAttachments.AnyAsync(x => x.ComplaintId == id);
        if (!alreadyHasEvidence && attachments.Count == 0 && string.IsNullOrWhiteSpace(NoEvidenceReason))
        {
            TempData["Error"] = "กรุณาแนบหลักฐานหรือระบุเหตุผลที่ไม่มีหลักฐาน";
            return RedirectToPage(new { id });
        }

        try
        {
            await complaintService.AddNoteAsync(
                id, GetStaffId(), "InternalNote", InvestigationNote.Trim(), attachments);
            await complaintService.CompleteInvestigationAsync(
                id, InvestigationSummary ?? string.Empty, NoEvidenceReason, GetStaffId());
            TempData["Success"] = "บันทึกผลการตรวจสอบครบถ้วนแล้ว ไปยังขั้นสรุปผล";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPrepareClosureAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        if (!ClosureChecklistConfirmed)
        {
            TempData["Error"] = "กรุณาตรวจสอบและยืนยัน checklist ก่อนแจ้งผล";
            return RedirectToPage(new { id });
        }
        if (string.IsNullOrWhiteSpace(CloseOpening) ||
            !CloseOpeningOptions.TryGetValue(CloseOpening, out var openingSentence))
        {
            TempData["Error"] = "กรุณาเลือกประโยคขึ้นต้นสำหรับสรุปผล";
            return RedirectToPage(new { id });
        }
        if (string.IsNullOrWhiteSpace(CloseResolution))
        {
            TempData["Error"] = "กรุณากรอกรายละเอียดสรุปผล";
            return RedirectToPage(new { id });
        }
        try
        {
            await complaintService.PrepareClosureAsync(
                id, $"{openingSentence} {CloseResolution?.Trim()}", GetStaffId());
            TempData["Success"] = "ยืนยันสรุปผลแล้ว กรุณาตรวจข้อความและแจ้งผลในขั้นสุดท้าย";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostClosePreparedAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        try
        {
            await complaintService.ClosePreparedAsync(id, GetStaffId());
            TempData["Success"] = "แจ้งผลและปิดเรื่องเรียบร้อยแล้ว";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPreviousWorkflowStepAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        try
        {
            await complaintService.MoveToPreviousWorkflowStepAsync(id, GetStaffId());
            TempData["Success"] = "ย้อนกลับไปขั้นตอนก่อนหน้าแล้ว ข้อมูลเดิมยังคงอยู่เพื่อให้ตรวจสอบและแก้ไขได้";
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

    public async Task<IActionResult> OnPostUpdateClassificationAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        if (ClassificationCategoryId <= 0)
        {
            TempData["Error"] = "กรุณาเลือกประเภทเรื่อง";
            return RedirectToPage(new { id });
        }

        var selectedDepartment = ClassificationDepartment?.Trim();
        if (!string.IsNullOrWhiteSpace(selectedDepartment))
        {
            var currentDepartment = await db.Complaints
                .Where(x => x.Id == id)
                .Select(x => x.ResponsibleDepartment)
                .FirstOrDefaultAsync();
            var departmentIsAllowed = selectedDepartment == currentDepartment ||
                await db.ComplaintDepartments.AnyAsync(x => x.IsActive && x.Name == selectedDepartment);
            if (!departmentIsAllowed)
            {
                TempData["Error"] = "ฝ่ายรับผิดชอบที่เลือกไม่พร้อมใช้งาน กรุณาเลือกใหม่";
                return RedirectToPage(new { id });
            }
        }
        else
        {
            var categoryDepartment = await db.ComplaintCategories
                .Where(x => x.Id == ClassificationCategoryId && x.IsActive)
                .Select(x => x.DepartmentName)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(categoryDepartment) &&
                !await db.ComplaintDepartments.AnyAsync(x => x.IsActive && x.Name == categoryDepartment))
            {
                TempData["Error"] = "ฝ่ายเริ่มต้นของประเภทเรื่องนี้ถูกปิดใช้งาน กรุณาเลือกฝ่ายรับผิดชอบใหม่";
                return RedirectToPage(new { id });
            }
        }

        try
        {
            await complaintService.UpdateClassificationAsync(
                id,
                ClassificationCategoryId,
                ClassificationSubCategoryId > 0 ? ClassificationSubCategoryId : null,
                selectedDepartment,
                GetStaffId());
            TempData["Success"] = "แก้ไขประเภทเรื่องและฝ่ายรับผิดชอบเรียบร้อยแล้ว";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCloseAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        try
        {
            await complaintService.ClosePreparedAsync(id, GetStaffId());
            TempData["Success"] = "แจ้งผลและปิดเรื่องเรียบร้อยแล้ว";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReopenAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        await complaintService.ReopenAsync(id, GetStaffId());
        TempData["Success"] = "เปิดเรื่องใหม่เรียบร้อย กลับไปขั้นตอนที่ 2 เพื่อตรวจสอบข้อมูลอีกครั้ง";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSendToEdocAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();

        try
        {
            await edocIntegrationService.QueueAsync(id, GetStaffId(), EdocBasketId);
            var link = await edocIntegrationService.SendNowAsync(id);
            if (link.SyncStatus == EdocSyncStatuses.Sent)
                TempData["Success"] = $"สร้างเอกสาร eDOC สำเร็จ เลขทะเบียน {link.RegisterNumber ?? "กำลังรอเลขทะเบียน"}";
            else if (link.SyncStatus == EdocSyncStatuses.SentWithAttachmentErrors)
                TempData["Error"] = "สร้างเอกสาร eDOC แล้ว แต่มีไฟล์แนบบางรายการส่งไม่สำเร็จ กรุณาตรวจสอบรายละเอียด";
            else
                TempData["Error"] = "ยังยืนยันผลการสร้างเอกสาร eDOC ไม่ได้ กรุณาตรวจสอบสถานะก่อนลองใหม่";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostTrackEdocAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        try
        {
            await edocIntegrationService.TrackAsync(id);
            TempData["Success"] = "อัปเดตสถานะจาก eDOC แล้ว";
        }
        catch (Exception ex) when (ex is InvalidOperationException or EdocApiException)
        {
            TempData["Error"] = "ไม่สามารถตรวจสอบสถานะ eDOC ได้ กรุณาลองใหม่หรือติดต่อผู้ดูแลระบบ";
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReconcileEdocAsync(int id)
    {
        if (!await CanModifyAsync(id)) return Forbid();
        try
        {
            var link = await edocIntegrationService.ReconcileAsync(id);
            TempData[link.SyncStatus == EdocSyncStatuses.Sent ? "Success" : "Error"] =
                link.SyncStatus == EdocSyncStatuses.Sent
                    ? "พบเอกสารใน eDOC และปรับสถานะให้ถูกต้องแล้ว"
                    : "ยังไม่พบเอกสารใน eDOC กรุณาตรวจสอบข้อมูลก่อนสั่งส่งใหม่";
        }
        catch (Exception ex) when (ex is InvalidOperationException or EdocApiException)
        {
            TempData["Error"] = "ไม่สามารถตรวจสอบเอกสารกับ eDOC ได้ กรุณาลองใหม่หรือติดต่อผู้ดูแลระบบ";
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRetryEdocAsync(int id)
    {
        if (!User.IsInRole("SuperAdmin")) return Forbid();
        try
        {
            await edocIntegrationService.RetryAsync(id, GetStaffId());
            var link = await edocIntegrationService.SendNowAsync(id);
            TempData[link.SyncStatus == EdocSyncStatuses.Sent ? "Success" : "Error"] =
                link.SyncStatus == EdocSyncStatuses.Sent
                    ? "ส่งเอกสาร eDOC ใหม่สำเร็จแล้ว"
                    : "การส่งใหม่ยังไม่สำเร็จ กรุณาตรวจสอบรายละเอียด";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRetryEdocAttachmentsAsync(int id)
    {
        if (!User.IsInRole("SuperAdmin")) return Forbid();
        try
        {
            var link = await edocIntegrationService.RetryAttachmentsAsync(id, GetStaffId());
            TempData[link.SyncStatus == EdocSyncStatuses.Sent ? "Success" : "Error"] =
                link.SyncStatus == EdocSyncStatuses.Sent
                    ? "ส่งไฟล์แนบไปยัง eDOC ครบแล้ว"
                    : "ยังมีไฟล์แนบบางรายการส่งไม่สำเร็จ กรุณาตรวจสอบรายละเอียด";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDownloadPdfAsync(int id)
    {
        var complaint = await complaintService.GetByIdAsync(id);
        if (complaint == null) return NotFound();

        var recipientDepartment = await ResolvePdfDepartmentAsync(complaint);
        if (recipientDepartment is null)
        {
            TempData["Error"] = "กรุณาเลือกฝ่ายงานที่เกี่ยวข้องก่อนสร้างเอกสาร PDF";
            return RedirectToPage(new { id });
        }

        // The server decides the PDF privacy mode from the citizen's stored choice.
        // Legacy/unknown records fall back to masked; posted form values cannot override it.
        var maskReporter = ReporterPrivacyChoices.ShouldMask(complaint.ReporterPrivacyChoice);
        var officerName = User.FindFirstValue("FullName") ?? User.FindFirstValue(ClaimTypes.Name);
        var signer = await pdfSignerSettings.GetProfileAsync();
        var pdf = pdfExportService.GenerateComplaintPdf(
            complaint, officerName, maskReporter, signer, recipientDepartment);
        await auditService.LogAsync("ExportComplaintPdf", GetStaffId(),
            User.FindFirstValue("EmployeeCode"), "Complaint", complaint.Id.ToString(),
            new { complaint.ReferenceNumber, maskReporter, recipientDepartment },
            HttpContext.Connection.RemoteIpAddress?.ToString());
        var suffix = maskReporter ? "-masked" : "";
        return File(pdf, "application/pdf", $"complaint-{complaint.ReferenceNumber}{suffix}.pdf");
    }

    public async Task<IActionResult> OnPostDownloadExcelAsync(int id)
    {
        var complaint = await complaintService.GetByIdAsync(id);
        if (complaint == null) return NotFound();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("รายละเอียดเรื่องร้องเรียน");
        var rows = SingleComplaintExportRows(complaint);
        for (var index = 0; index < rows.Count; index++)
        {
            sheet.Cell(index + 1, 1).Value = rows[index].Label;
            sheet.Cell(index + 1, 1).Style.Font.Bold = true;
            sheet.Cell(index + 1, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF8");
            sheet.Cell(index + 1, 2).Value = rows[index].Value;
        }
        sheet.Column(1).Width = 24;
        sheet.Column(2).Width = 70;
        sheet.Column(2).Style.Alignment.WrapText = true;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        await auditService.LogAsync("ExportComplaintExcel", GetStaffId(),
            User.FindFirstValue("EmployeeCode"), "Complaint", complaint.Id.ToString(),
            new { complaint.ReferenceNumber, containsPersonalData = true },
            HttpContext.Connection.RemoteIpAddress?.ToString());
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"complaint-{complaint.ReferenceNumber}.xlsx");
    }

    public async Task<IActionResult> OnPostDownloadWordAsync(int id)
    {
        var complaint = await complaintService.GetByIdAsync(id);
        if (complaint == null) return NotFound();

        var html = new StringBuilder("<html><head><meta charset='utf-8'><style>")
            .Append("body{font-family:'Sarabun','Tahoma',sans-serif;font-size:12pt;color:#1f2937}h1{color:#0D2E6E}")
            .Append("table{width:100%;border-collapse:collapse}th,td{border:1px solid #d1d5db;padding:8px;text-align:left;vertical-align:top}th{width:25%;background:#E8EEF8}")
            .Append("</style></head><body><h1>รายละเอียดเรื่องร้องเรียน</h1><table>");
        foreach (var row in SingleComplaintExportRows(complaint))
            html.Append($"<tr><th>{WebUtility.HtmlEncode(row.Label)}</th><td>{WebUtility.HtmlEncode(row.Value).Replace("\n", "<br>")}</td></tr>");
        html.Append("</table></body></html>");

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(html.ToString())).ToArray();
        await auditService.LogAsync("ExportComplaintWord", GetStaffId(),
            User.FindFirstValue("EmployeeCode"), "Complaint", complaint.Id.ToString(),
            new { complaint.ReferenceNumber, containsPersonalData = true },
            HttpContext.Connection.RemoteIpAddress?.ToString());
        return File(bytes, "application/msword", $"complaint-{complaint.ReferenceNumber}.doc");
    }

    private async Task<IActionResult> LoadComplaintAsync(int id)
    {
        var complaint = await complaintService.GetByIdAsync(id);
        if (complaint == null) return NotFound();
        Complaint = complaint;
        EdocLink = await edocIntegrationService.GetByComplaintIdAsync(id);
        EdocBaskets = edocIntegrationService.GetBaskets();
        DefaultEdocBasketId = edocIntegrationService.GetDefaultBasketId(complaint.CategoryId);
        EdocBasketId = DefaultEdocBasketId;
        ClassificationCategoryId = complaint.CategoryId;
        ClassificationSubCategoryId = complaint.SubCategoryId;
        ClassificationDepartment = complaint.ResponsibleDepartment ?? complaint.Category.DepartmentName;
        ViewData["Title"] = $"เรื่อง {complaint.ReferenceNumber}";

        var departmentOptions = await db.ComplaintDepartments
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => x.Name)
            .ToListAsync();
        var activeDepartmentNames = departmentOptions.ToHashSet(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(ClassificationDepartment) && !departmentOptions.Contains(ClassificationDepartment))
            departmentOptions.Add(ClassificationDepartment);
        PdfDepartmentOptions = departmentOptions;

        ClassificationCategoryOptions = await db.ComplaintCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
            .ToListAsync();
        ClassificationDepartmentOptions = PdfDepartmentOptions;
        var categoryDepartments = await db.ComplaintCategories
            .Where(c => c.IsActive)
            .Select(c => new { c.Id, c.DepartmentName })
            .ToListAsync();
        ClassificationDepartmentsByCategoryJson = JsonSerializer.Serialize(
            categoryDepartments.ToDictionary(
                c => c.Id.ToString(),
                c => c.DepartmentName != null && activeDepartmentNames.Contains(c.DepartmentName)
                    ? c.DepartmentName
                    : null));

        var classificationSubCategories = await db.ComplaintSubCategories
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .Select(s => new { s.CategoryId, s.Id, s.Name })
            .ToListAsync();
        ClassificationSubCategoriesJson = JsonSerializer.Serialize(
            classificationSubCategories
                .GroupBy(s => s.CategoryId)
                .ToDictionary(
                    group => group.Key.ToString(),
                    group => group.Select(s => new { id = s.Id, name = s.Name }).ToList()));

        StaffOptions = await db.StaffUsers
            .Where(u => u.IsActive && (u.Role == "GeneralOfficer" || u.Role == "SuperAdmin")
                     && u.Id != complaint.AssignedToId)
            .OrderBy(u => u.FullName)
            .Select(u => new SelectListItem($"{u.FullName} ({u.EmployeeCode})", u.Id.ToString()))
            .ToListAsync();

        return Page();
    }

    private static IReadOnlyList<(string Label, string Value)> SingleComplaintExportRows(Models.Complaint complaint) =>
    [
        ("เลขที่อ้างอิง", complaint.ReferenceNumber),
        ("วันที่รับเรื่อง", complaint.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")),
        ("ประเภทเรื่อง", complaint.Category?.Name ?? "-"),
        ("หัวข้อย่อย", complaint.SubCategory?.Name ?? "-"),
        ("ฝ่ายรับผิดชอบ", complaint.ResponsibleDepartment ?? complaint.Category?.DepartmentName ?? "-"),
        ("สถานะ", StatusLabel(complaint.Status)),
        ("ผู้ร้องเรียน", complaint.ReporterName),
        ("โทรศัพท์", complaint.ReporterPhone),
        ("อีเมล", complaint.ReporterEmail ?? "-"),
        ("วัตถุประสงค์", ComplaintSubmissionPurposes.Label(complaint.SubmissionPurpose)),
        ("รายละเอียด", complaint.Description),
        ("เจ้าหน้าที่ผู้รับผิดชอบ", complaint.AssignedTo?.FullName ?? "-")
    ];

    private async Task<string?> ResolvePdfDepartmentAsync(Models.Complaint complaint)
    {
        if (PdfDepartmentName == "__other")
        {
            var other = PdfDepartmentOther?.Trim();
            return string.IsNullOrWhiteSpace(other) || other.Length > 200 ? null : other;
        }

        var selected = PdfDepartmentName?.Trim();
        if (string.IsNullOrWhiteSpace(selected) || selected.Length > 200) return null;

        var currentDepartment = complaint.ResponsibleDepartment ?? complaint.Category?.DepartmentName;
        return selected == currentDepartment || await db.ComplaintDepartments.AnyAsync(x =>
            x.IsActive && x.Name == selected)
            ? selected
            : null;
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

    public static string StatusLabel(string s) => ComplaintStatusLabels.Thai(s);

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
