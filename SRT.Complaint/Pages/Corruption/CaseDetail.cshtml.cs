#nullable enable
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRT.Complaint.Models;
using SRT.Complaint.Models.Corruption;
using SRT.Complaint.Services;

namespace SRT.Complaint.Pages.Corruption;

[Authorize(Policy = "CorruptionAccess")]
public class CaseDetailModel(
    ICorruptionService corruptionService,
    IPdfExportService pdfExportService,
    IStaffDirectoryService staffDirectory) : PageModel
{
    // Action bindings
    [BindProperty] public string?  InvLogContent        { get; set; }
    [BindProperty] public bool     InvLogIsConfidential { get; set; } = true;
    [BindProperty] public string?  NewStatus            { get; set; }
    [BindProperty] public string?  CloseResolution      { get; set; }
    [BindProperty] public string?  DecryptReason        { get; set; }

    // Page data
    public CorruptionReport       Report        { get; private set; } = null!;
    public Dictionary<int, string> StaffNames   { get; private set; } = new();
    public DecryptedReporterInfo?  DecryptedInfo { get; private set; }
    public string[]                AllowedNextStatuses { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        ViewData["Title"] = "สืบสวนเรื่องทุจริต";
        return await LoadAsync(id);
    }

    // ─── Claim ───
    public async Task<IActionResult> OnPostClaimAsync(int id)
    {
        try
        {
            await corruptionService.ClaimAsync(id, GetStaffId());
            TempData["Success"] = "รับเรื่องเรียบร้อยแล้ว";
        }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToPage(new { id });
    }

    // ─── Add Investigation Log ───
    public async Task<IActionResult> OnPostAddLogAsync(int id)
    {
        if (string.IsNullOrWhiteSpace(InvLogContent))
        {
            TempData["Error"] = "กรุณากรอกเนื้อหาบันทึก";
            return RedirectToPage(new { id });
        }
        await corruptionService.AddInvestigationLogAsync(id, GetStaffId(), InvLogContent.Trim(), InvLogIsConfidential);
        TempData["Success"] = "บันทึกการสืบสวนสำเร็จ";
        return RedirectToPage(new { id });
    }

    // ─── Update Status ───
    public async Task<IActionResult> OnPostUpdateStatusAsync(int id)
    {
        if (string.IsNullOrEmpty(NewStatus))
        {
            TempData["Error"] = "กรุณาเลือกสถานะ";
            return RedirectToPage(new { id });
        }
        await corruptionService.UpdateStatusAsync(id, NewStatus, GetStaffId(), null);
        TempData["Success"] = $"อัปเดตสถานะเป็น \"{StatusLabel(NewStatus)}\" เรียบร้อย";
        return RedirectToPage(new { id });
    }

    // ─── Send Back to Investigation ───
    public async Task<IActionResult> OnPostSendBackAsync(int id)
    {
        await corruptionService.UpdateStatusAsync(id, "InProgress", GetStaffId(), "ส่งกลับสืบสวนเพิ่มเติม");
        TempData["Success"] = "ส่งกลับสืบสวนเรียบร้อยแล้ว";
        return RedirectToPage(new { id });
    }

    // ─── Close ───
    public async Task<IActionResult> OnPostCloseAsync(int id)
    {
        if (string.IsNullOrWhiteSpace(CloseResolution))
        {
            TempData["Error"] = "กรุณากรอกสรุปการสืบสวนก่อนปิดเรื่อง";
            return RedirectToPage(new { id });
        }
        try
        {
            await corruptionService.CloseAsync(id, CloseResolution.Trim(), GetStaffId());
            TempData["Success"] = "ปิดเรื่องเรียบร้อยแล้ว";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"เกิดข้อผิดพลาด: {ex.Message}";
        }
        return RedirectToPage(new { id });
    }

    // ─── Reopen ───
    public async Task<IActionResult> OnPostReopenAsync(int id)
    {
        await corruptionService.ReopenAsync(id, GetStaffId());
        TempData["Success"] = "เปิดเรื่องใหม่เรียบร้อย กลับสู่การสืบสวนอีกครั้ง";
        return RedirectToPage(new { id });
    }

    // ─── Decrypt Reporter Info ───
    // Returns Page() (not redirect) so decrypted data lives only in this request's memory.
    // Refresh = masked again. No TempData = no server-side caching of sensitive data.
    public async Task<IActionResult> OnPostDecryptAsync(int id)
    {
        if (string.IsNullOrWhiteSpace(DecryptReason))
        {
            TempData["Error"] = "กรุณาระบุเหตุผลในการขอดูข้อมูล";
            return RedirectToPage(new { id });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        DecryptedInfo = await corruptionService.DecryptReporterInfoAsync(
            id, GetStaffId(), DecryptReason.Trim(), ipAddress);

        return await LoadAsync(id);   // reload report + return Page() with DecryptedInfo set
    }

    // ─── Download PDF ───
    public async Task<IActionResult> OnPostDownloadPdfAsync(int id)
    {
        var report = await corruptionService.GetByIdAsync(id);
        if (report == null) return NotFound();

        var officerName = User.FindFirstValue("FullName") ?? User.FindFirstValue(ClaimTypes.Name);
        var pdf = pdfExportService.GenerateCorruptionReportPdf(report, officerName);
        return File(pdf, "application/pdf", $"corruption-{report.ReferenceNumber}.pdf");
    }

    // ─── Helpers ───
    private async Task<IActionResult> LoadAsync(int id)
    {
        var report = await corruptionService.GetByIdAsync(id);
        if (report == null) return NotFound();
        Report = report;
        ViewData["Title"] = $"ทุจริต {report.ReferenceNumber}";

        AllowedNextStatuses = report.Status switch
        {
            "Pending"    => ["InProgress"],
            "InProgress" => ["UnderReview", "Rejected"],
            "UnderReview" => ["Rejected"],
            _            => []
        };

        // Collect all staff IDs referenced in this report (across contexts)
        var staffIds = new HashSet<int>();
        if (report.AssignedToId.HasValue) staffIds.Add(report.AssignedToId.Value);
        foreach (var log  in report.InvestigationLogs) staffIds.Add(log.AuthorId);
        foreach (var dlog in report.DecryptionLogs)    staffIds.Add(dlog.RequestedById);

        StaffNames = await staffDirectory.GetNamesByIdsAsync(staffIds);

        return Page();
    }

    private int GetStaffId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public static string StatusLabel(string s) => s switch
    {
        "Pending"     => "รอดำเนินการ",
        "InProgress"  => "กำลังสืบสวน",
        "UnderReview" => "อยู่ระหว่างพิจารณา",
        "Closed"      => "ปิดเรื่อง",
        "Rejected"    => "ไม่รับเรื่อง",
        _             => s
    };
}
