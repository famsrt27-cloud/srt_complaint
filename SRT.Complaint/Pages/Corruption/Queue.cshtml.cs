#nullable enable
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRT.Complaint.Services;
using SRT.Complaint.Data;
using Microsoft.EntityFrameworkCore;

namespace SRT.Complaint.Pages.Corruption;

[Authorize(Policy = "CorruptionAccess")]
public class QueueModel(
    ICorruptionService corruptionService,
    IStaffDirectoryService staffDirectory,
    AppDbContext appDb) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? QuickView { get; set; }
    [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;

    public const int PageSize = 20;
    public IReadOnlyList<CorruptionQueueRowVm> Reports { get; private set; } = [];
    public int TotalCount { get; private set; }
    public int TotalPages { get; private set; }
    public int PageStart => TotalCount == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
    public int PageEnd   => Math.Min(CurrentPage * PageSize, TotalCount);

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "คิวเรื่องทุจริต";

        var filter = new CorruptionQueueFilter(
            Status: Status,
            Search: Search,
            QuickView: QuickView,
            ViewerStaffId: int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            Page: CurrentPage,
            PageSize: PageSize);

        var reports = await corruptionService.GetQueueFilteredAsync(filter);
        TotalCount = await corruptionService.GetTotalCountAsync(filter);
        TotalPages = (int)Math.Ceiling((double)TotalCount / PageSize);

        // Staff names resolved via IStaffDirectoryService (no FK across CorruptionDbContext/AppDbContext)
        var assignedIds = reports
            .Where(r => r.AssignedToId.HasValue)
            .Select(r => r.AssignedToId!.Value);

        var staffNames = await staffDirectory.GetNamesByIdsAsync(assignedIds);

        var now = DateTime.UtcNow;
        var normalSla = await appDb.SlaConfigs.AsNoTracking().FirstOrDefaultAsync(x => x.Priority == "Normal");
        var warningWindowHours = normalSla is null
            ? 24
            : Math.Max(1, normalSla.ResolutionHours * (100 - normalSla.WarningThresholdPercent) / 100d);
        Reports = reports.Select(r => new CorruptionQueueRowVm(
            r.Id,
            r.ReferenceNumber,
            r.SubjectType,
            r.SubjectPersonName,
            r.SubjectDepartment,
            r.ReporterNameMasked,
            r.ReporterPhoneMasked,
            r.Priority,
            PriorityLabel(r.Priority),
            r.Status,
            StatusLabel(r.Status),
            r.CreatedAt,
            r.AssignedToId,
            r.AssignedToId.HasValue ? staffNames.GetValueOrDefault(r.AssignedToId.Value) : null,
            r.SlaBreached,
            !r.SlaBreached && r.SlaDeadline.HasValue && r.SlaDeadline <= now.AddHours(warningWindowHours),
            SlaText(r.SlaDeadline, now)
        )).ToList();
    }

    public async Task<IActionResult> OnPostClaimAsync(int id)
    {
        var staffId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        try
        {
            await corruptionService.ClaimAsync(id, staffId);
            TempData["Success"] = "รับเรื่องเรียบร้อยแล้ว";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage(new { Status, Search, QuickView, CurrentPage });
    }

    private static string PriorityLabel(string p) => p switch
    {
        "Critical" => "เร่งด่วนมาก",
        "Urgent"   => "เร่งด่วน",
        "High"     => "สำคัญ",
        "Normal"   => "ปกติ",
        _          => p
    };

    private static string StatusLabel(string s) => s switch
    {
        "Pending"         => "รอดำเนินการ",
        "InProgress"      => "กำลังสืบสวน",
        "UnderReview"     => "อยู่ระหว่างพิจารณา",
        "Closed"          => "ปิดเรื่อง",
        "Rejected"        => "ไม่รับเรื่อง",
        _                 => s
    };

    private static string SlaText(DateTime? deadline, DateTime now)
    {
        if (!deadline.HasValue) return "-";
        if (deadline < now) return "เกิน SLA แล้ว";
        var diff = deadline.Value - now;
        if (diff.TotalHours < 1)  return $"เหลือ {(int)diff.TotalMinutes} นาที";
        if (diff.TotalDays  < 1)  return $"เหลือ {(int)diff.TotalHours} ชม.";
        return $"เหลือ {(int)diff.TotalDays} วัน";
    }
}

public record CorruptionQueueRowVm(
    int Id,
    string ReferenceNumber,
    string SubjectType,
    string? SubjectPersonName,
    string? SubjectDepartment,
    string ReporterNameMasked,
    string ReporterPhoneMasked,
    string Priority,
    string PriorityLabel,
    string Status,
    string StatusLabel,
    DateTime CreatedAt,
    int? AssignedToId,
    string? AssignedToName,
    bool SlaBreached,
    bool SlaWarning,
    string SlaRemainingText
);
