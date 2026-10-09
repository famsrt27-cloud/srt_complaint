#nullable enable
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Models;
using SRT.Complaint.Services;


namespace SRT.Complaint.Pages.Admin;

[Authorize(Policy = "SuperAdmin")]
public class UsersModel(AppDbContext db, IStaffUserService staffUserService) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search      { get; set; }
    [BindProperty(SupportsGet = true)] public string? RoleFilter  { get; set; }
    [BindProperty(SupportsGet = true)] public int     CurrentPage { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int?    EditId      { get; set; }

    public const int PageSize = 20;
    public IReadOnlyList<StaffUser> Users  { get; private set; } = [];
    public int TotalCount { get; private set; }
    public int TotalPages { get; private set; }
    public int PageStart  => TotalCount == 0 ? 0 : (CurrentPage - 1) * PageSize + 1;
    public int PageEnd    => Math.Min(CurrentPage * PageSize, TotalCount);
    public StaffUser? EditTarget { get; private set; }

    // Create
    [BindProperty] public string? NewEmployeeCode { get; set; }
    [BindProperty] public string? NewFullName     { get; set; }
    [BindProperty] public string? NewEmail        { get; set; }
    [BindProperty] public string? NewRole         { get; set; }

    // Edit
    [BindProperty] public int     EditUserId   { get; set; }
    [BindProperty] public string? EditFullName { get; set; }
    [BindProperty] public string? EditEmail    { get; set; }
    [BindProperty] public string? EditRole     { get; set; }
    [BindProperty] public bool    EditIsActive { get; set; }

    // Reset password
    [BindProperty] public int ResetUserId { get; set; }

    // View password
    [BindProperty] public int ViewPasswordUserId { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "จัดการเจ้าหน้าที่";
        await LoadListAsync();
        if (EditId.HasValue)
            EditTarget = await db.StaffUsers.FindAsync(EditId.Value);
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var result = await staffUserService.CreateAsync(NewEmployeeCode ?? "", NewFullName ?? "", NewEmail,
            NewRole ?? "", GetActorId(), GetActorCode(), GetIp());
        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToPage();
        }

        TempData["Success"]       = $"สร้างบัญชี {result.User!.FullName} ({result.User.EmployeeCode}) เรียบร้อยแล้ว";
        TempData["TempPassword"]  = result.TempPassword;
        TempData["TempPwTarget"]  = $"{result.User.FullName} ({result.User.EmployeeCode})";
        TempData["TempPwExpires"] = result.TempPasswordExpiresAt!.Value.ToLocalTime().ToString("HH:mm น. (dd/MM/yyyy)");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync()
    {
        var result = await staffUserService.EditAsync(EditUserId, EditFullName ?? "", EditEmail, EditRole ?? "",
            EditIsActive, GetActorId(), GetActorCode(), GetIp());
        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToPage(new { editId = EditUserId });
        }

        TempData["Success"] = $"แก้ไขข้อมูล {result.User!.FullName} เรียบร้อยแล้ว";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync()
    {
        var result = await staffUserService.ResetPasswordAsync(ResetUserId, GetActorId(), GetActorCode(), GetIp());
        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToPage();
        }

        TempData["Success"]       = $"รีเซ็ตรหัสผ่านของ {result.User!.FullName} เรียบร้อยแล้ว";
        TempData["TempPassword"]  = result.TempPassword;
        TempData["TempPwTarget"]  = result.User.FullName;
        TempData["TempPwExpires"] = result.TempPasswordExpiresAt!.Value.ToLocalTime().ToString("HH:mm น. (dd/MM/yyyy)");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostViewPasswordAsync()
    {
        var result = await staffUserService.ViewTempPasswordAsync(ViewPasswordUserId);
        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToPage();
        }

        TempData["TempPassword"]  = result.TempPassword;
        TempData["TempPwTarget"]  = $"{result.User!.FullName} ({result.User.EmployeeCode})";
        TempData["TempPwExpires"] = result.TempPasswordExpiresAt!.Value.ToLocalTime().ToString("HH:mm น. (dd/MM/yyyy)");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var result = await staffUserService.ToggleActiveAsync(id, GetActorId(), GetActorCode(), GetIp());
        if (!result.Success)
        {
            TempData["Error"] = result.Error;
            return RedirectToPage(new { Search, RoleFilter, CurrentPage });
        }

        TempData["Success"] = $"{(result.User!.IsActive ? "เปิดใช้งาน" : "ปิดใช้งาน")} {result.User.FullName} เรียบร้อย";
        return RedirectToPage(new { Search, RoleFilter, CurrentPage });
    }

    private async Task LoadListAsync()
    {
        var q = db.StaffUsers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(Search))
        {
            var s = Search.Trim();
            q = q.Where(u => u.FullName.Contains(s) || u.EmployeeCode.Contains(s));
        }
        if (!string.IsNullOrWhiteSpace(RoleFilter))
            q = q.Where(u => u.Role == RoleFilter);

        TotalCount = await q.CountAsync();
        TotalPages = (int)Math.Ceiling((double)TotalCount / PageSize);
        if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

        Users = await q
            .OrderBy(u => u.EmployeeCode)
            .Skip((CurrentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();
    }

    private int    GetActorId()   => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string GetActorCode() => User.FindFirstValue("EmployeeCode") ?? "";
    private string GetIp()        => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public static string RoleLabel(string r) => r switch
    {
        "GeneralOfficer"    => "เจ้าหน้าที่ทั่วไป",
        "CorruptionOfficer" => "เจ้าหน้าที่ทุจริต",
        "SuperAdmin"        => "ผู้ดูแลระบบ",
        _                   => r
    };
}
