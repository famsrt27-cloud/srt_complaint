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
public class DepartmentsModel(AppDbContext db, IAuditService auditService) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public int? EditId { get; set; }

    [BindProperty] public string? NewName { get; set; }
    [BindProperty] public string? NewDescription { get; set; }
    [BindProperty] public int NewSortOrder { get; set; }

    [BindProperty] public int EditDepartmentId { get; set; }
    [BindProperty] public string? EditName { get; set; }
    [BindProperty] public string? EditDescription { get; set; }
    [BindProperty] public int EditSortOrder { get; set; }

    public IReadOnlyList<DepartmentListItem> Departments { get; private set; } = [];
    public ComplaintDepartment? EditTarget { get; private set; }
    public int TotalCount { get; private set; }
    public int ActiveCount { get; private set; }
    public int InactiveCount { get; private set; }
    public int NextSortOrder { get; private set; } = 1;

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "ฝ่ายที่เกี่ยวข้อง";

        var allDepartments = db.ComplaintDepartments.AsNoTracking();
        TotalCount = await allDepartments.CountAsync();
        ActiveCount = await allDepartments.CountAsync(x => x.IsActive);
        InactiveCount = TotalCount - ActiveCount;
        NextSortOrder = (await allDepartments.MaxAsync(x => (int?)x.SortOrder) ?? 0) + 1;

        var query = allDepartments.AsQueryable();
        var search = Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Name.Contains(search) || (x.Description != null && x.Description.Contains(search)));

        query = Status switch
        {
            "active" => query.Where(x => x.IsActive),
            "inactive" => query.Where(x => !x.IsActive),
            _ => query
        };

        Departments = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new DepartmentListItem(
                x.Id,
                x.Name,
                x.Description,
                x.IsActive,
                x.SortOrder,
                db.ComplaintCategories.Count(c => c.DepartmentName == x.Name),
                db.Complaints.Count(c => c.ResponsibleDepartment == x.Name)))
            .ToListAsync();

        if (EditId.HasValue)
            EditTarget = await db.ComplaintDepartments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == EditId.Value);
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var name = NewName?.Trim();
        var description = NormalizeDescription(NewDescription);
        var error = ValidateInput(name, description);
        if (error is not null)
            return ErrorRedirect(error);

        if (await db.ComplaintDepartments.AnyAsync(x => x.Name == name))
            return ErrorRedirect($"มีฝ่าย \"{name}\" อยู่ในระบบแล้ว");

        var department = new ComplaintDepartment
        {
            Name = name!,
            Description = description,
            SortOrder = Math.Max(0, NewSortOrder),
            IsActive = true
        };
        db.ComplaintDepartments.Add(department);
        await db.SaveChangesAsync();
        await LogAsync("CreateDepartment", department.Id, new { department.Name, department.SortOrder });

        TempData["Success"] = $"เพิ่มฝ่าย \"{department.Name}\" เรียบร้อยแล้ว";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync()
    {
        var name = EditName?.Trim();
        var description = NormalizeDescription(EditDescription);
        var error = ValidateInput(name, description);
        if (error is not null)
            return ErrorRedirect(error, EditDepartmentId);

        var department = await db.ComplaintDepartments.FindAsync(EditDepartmentId);
        if (department is null) return NotFound();

        if (await db.ComplaintDepartments.AnyAsync(x => x.Id != department.Id && x.Name == name))
            return ErrorRedirect($"มีฝ่าย \"{name}\" อยู่ในระบบแล้ว", department.Id);

        var oldName = department.Name;
        var categories = oldName == name
            ? []
            : await db.ComplaintCategories.Where(x => x.DepartmentName == oldName).ToListAsync();
        var complaints = oldName == name
            ? []
            : await db.Complaints.Where(x => x.ResponsibleDepartment == oldName).ToListAsync();

        department.Name = name!;
        department.Description = description;
        department.SortOrder = Math.Max(0, EditSortOrder);
        department.UpdatedAt = DateTime.UtcNow;
        foreach (var category in categories)
            category.DepartmentName = department.Name;
        foreach (var complaint in complaints)
            complaint.ResponsibleDepartment = department.Name;

        await db.SaveChangesAsync();
        await LogAsync("EditDepartment", department.Id, new
        {
            OldName = oldName,
            department.Name,
            department.SortOrder,
            UpdatedCategories = categories.Count,
            UpdatedComplaints = complaints.Count
        });

        TempData["Success"] = $"แก้ไขฝ่าย \"{department.Name}\" เรียบร้อยแล้ว";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var department = await db.ComplaintDepartments.FindAsync(id);
        if (department is null) return NotFound();

        department.IsActive = !department.IsActive;
        department.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await LogAsync(department.IsActive ? "ActivateDepartment" : "DeactivateDepartment", department.Id,
            new { department.Name });

        TempData["Success"] = $"{(department.IsActive ? "เปิดใช้งาน" : "ปิดใช้งาน")}ฝ่าย \"{department.Name}\" เรียบร้อยแล้ว";
        return RedirectToPage(new { Search, Status });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var department = await db.ComplaintDepartments.FindAsync(id);
        if (department is null) return NotFound();

        var categoryCount = await db.ComplaintCategories.CountAsync(x => x.DepartmentName == department.Name);
        var complaintCount = await db.Complaints.CountAsync(x => x.ResponsibleDepartment == department.Name);
        if (categoryCount > 0 || complaintCount > 0)
        {
            TempData["Error"] = $"ลบฝ่าย \"{department.Name}\" ไม่ได้ เพราะถูกใช้งานอยู่ กรุณาปิดใช้งานแทน";
            return RedirectToPage(new { Search, Status });
        }

        var name = department.Name;
        db.ComplaintDepartments.Remove(department);
        await db.SaveChangesAsync();
        await LogAsync("DeleteDepartment", id, new { Name = name });

        TempData["Success"] = $"ลบฝ่าย \"{name}\" เรียบร้อยแล้ว";
        return RedirectToPage(new { Search, Status });
    }

    private IActionResult ErrorRedirect(string message, int? editId = null)
    {
        TempData["Error"] = message;
        return RedirectToPage(new { Search, Status, EditId = editId });
    }

    private static string? ValidateInput(string? name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name)) return "กรุณากรอกชื่อฝ่าย";
        if (name.Length > 200) return "ชื่อฝ่ายต้องไม่เกิน 200 ตัวอักษร";
        if (description?.Length > 500) return "คำอธิบายต้องไม่เกิน 500 ตัวอักษร";
        return null;
    }

    private static string? NormalizeDescription(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private Task LogAsync(string action, int id, object detail) => auditService.LogAsync(
        action,
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
        User.FindFirstValue("EmployeeCode"),
        "ComplaintDepartment",
        id.ToString(),
        detail,
        HttpContext.Connection.RemoteIpAddress?.ToString());

    public sealed record DepartmentListItem(
        int Id,
        string Name,
        string? Description,
        bool IsActive,
        int SortOrder,
        int CategoryCount,
        int ComplaintCount)
    {
        public bool CanDelete => CategoryCount == 0 && ComplaintCount == 0;
    }
}
