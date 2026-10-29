#nullable enable
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Services;

namespace SRT.Complaint.Pages.Staff;

[Authorize(Policy = "StaffOnly")]
public class QueueModel(
    IComplaintService complaintService,
    IExternalSyncService externalSyncService,
    AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Priority { get; set; }
    [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? FromDate { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? ToDate { get; set; }
    [BindProperty(SupportsGet = true)] public int CurrentPage { get; set; } = 1;

    public const int PageSize = 20;
    public IReadOnlyList<ComplaintQueueRowVm> Complaints { get; private set; } = [];
    public int TotalCount { get; private set; }
    public int TotalPages { get; private set; }
    public int PageStart => TotalCount == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
    public int PageEnd => Math.Min(CurrentPage * PageSize, TotalCount);
    public IEnumerable<SelectListItem> CategoryOptions { get; private set; } = [];
    public IReadOnlyList<IExternalSystemAdapter> ExternalSystems { get; private set; } = [];

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "คิวเรื่องร้องเรียน";

        var filter = new ComplaintQueueFilter(
            Status: Status,
            CategoryId: CategoryId,
            Priority: Priority,
            FromDate: FromDate,
            ToDate: ToDate,
            Search: Search,
            Page: CurrentPage,
            PageSize: PageSize
        );

        var complaints = await complaintService.GetQueueAsync(filter);
        TotalCount = await complaintService.GetTotalCountAsync(filter);
        TotalPages = (int)Math.Ceiling((double)TotalCount / PageSize);

        var now = DateTime.UtcNow;
        Complaints = complaints.Select(c => new ComplaintQueueRowVm(
            c.Id,
            c.ReferenceNumber,
            c.Category.Name,
            c.ReporterName,
            c.Priority,
            PriorityLabel(c.Priority),
            c.Status,
            StatusLabel(c.Status),
            c.CreatedAt,
            c.AssignedToId,
            c.AssignedTo?.FullName,
            c.SlaBreached,
            !c.SlaBreached && c.SlaDeadline.HasValue && c.SlaDeadline <= now.AddHours(24),
            SlaText(c.SlaDeadline, now)
        )).ToList();

        CategoryOptions = await db.ComplaintCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
            .ToListAsync();

        ExternalSystems = externalSyncService.GetAvailableSystems();
    }

    public async Task<IActionResult> OnPostSyncAsync(string systemKey)
    {
        var staffId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var log = await externalSyncService.SyncAsync(systemKey, staffId);
        TempData["SyncResult"] = log.SyncStatus == "Success"
            ? $"success|{log.NewCount}|{log.DuplicateCount}|{log.FetchedCount}"
            : $"error|{log.ErrorMessage}";
        return RedirectToPage(new { Search, Status, Priority, CategoryId, FromDate, ToDate, CurrentPage });
    }

    public async Task<IActionResult> OnPostClaimAsync(int id)
    {
        var staffId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        try
        {
            await complaintService.ClaimAsync(id, staffId);
            TempData["Success"] = "รับเรื่องเรียบร้อยแล้ว";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage(new { Search, Status, Priority, CategoryId, FromDate, ToDate, CurrentPage });
    }

    private static string PriorityLabel(string p) => p switch
    {
        "Critical" => "เร่งด่วนมาก",
        "Urgent"   => "เร่งด่วน",
        "High"     => "สำคัญ",
        "Normal"   => "ปกติ",
        "Low"      => "ข้อเสนอแนะ",
        _          => p
    };

    private static string StatusLabel(string s) => s switch
    {
        "Pending"    => "รอรับเรื่อง",
        "InProgress" => "กำลังดำเนินการ",
        "Resolved"   => "รอสรุปผลปิดงาน",
        "Closed"     => "ปิดเรื่อง",
        "Rejected"   => "ปฏิเสธ",
        _            => s
    };

    private static string SlaText(DateTime? deadline, DateTime now)
    {
        if (!deadline.HasValue) return "-";
        if (deadline < now) return "เกิน SLA แล้ว";
        var diff = deadline.Value - now;
        if (diff.TotalHours < 1) return $"เหลือ {(int)diff.TotalMinutes} นาที";
        if (diff.TotalDays < 1) return $"เหลือ {(int)diff.TotalHours} ชม.";
        return $"เหลือ {(int)diff.TotalDays} วัน";
    }
}

public record ComplaintQueueRowVm(
    int Id,
    string ReferenceNumber,
    string CategoryName,
    string ReporterName,
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
