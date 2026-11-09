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
public class CategoriesModel(AppDbContext db, IAuditService auditService) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? EditId { get; set; }

    public IReadOnlyList<ComplaintCategory> Categories { get; private set; } = [];
    public Dictionary<int, List<ComplaintSubCategory>> SubsByCategory { get; private set; } = [];
    public IReadOnlyList<ComplaintDepartment> DepartmentOptions { get; private set; } = [];
    public ComplaintCategory? EditTarget { get; private set; }

    // Create category
    [BindProperty] public string? NewName           { get; set; }
    [BindProperty] public string? NewDepartmentName { get; set; }
    [BindProperty] public string  NewDefaultPriority { get; set; } = "Normal";
    [BindProperty] public int     NewSortOrder      { get; set; }

    // Edit category
    [BindProperty] public int     EditCatId            { get; set; }
    [BindProperty] public string? EditName             { get; set; }
    [BindProperty] public string? EditDepartmentName   { get; set; }
    [BindProperty] public string  EditDefaultPriority  { get; set; } = "Normal";
    [BindProperty] public int     EditSortOrder        { get; set; }
    [BindProperty] public bool    EditIsActive         { get; set; }

    // Create sub-category
    [BindProperty] public int     NewSubCategoryId   { get; set; }
    [BindProperty] public string? NewSubName         { get; set; }
    [BindProperty] public int     NewSubSortOrder    { get; set; }

    // Edit sub-category
    [BindProperty] public int     EditSubCatId       { get; set; }
    [BindProperty] public string? EditSubName        { get; set; }
    [BindProperty] public int     EditSubSortOrder   { get; set; }
    [BindProperty] public bool    EditSubIsActive     { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "หมวดหมู่เรื่องร้องเรียน";
        if (EditId.HasValue)
            EditTarget = await db.ComplaintCategories.FindAsync(EditId.Value);
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        Categories = await db.ComplaintCategories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync();
        var subs = await db.ComplaintSubCategories.OrderBy(s => s.SortOrder).ThenBy(s => s.Name).ToListAsync();
        SubsByCategory = subs.GroupBy(s => s.CategoryId).ToDictionary(g => g.Key, g => g.ToList());
        var currentDepartment = EditTarget?.DepartmentName;
        DepartmentOptions = await db.ComplaintDepartments
            .Where(x => x.IsActive || (currentDepartment != null && x.Name == currentDepartment))
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();
    }

    // ══ Category handlers ══

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName))
        {
            TempData["Error"] = "กรุณากรอกชื่อหมวดหมู่";
            return RedirectToPage();
        }
        var newDepartment = await ResolveDepartmentAsync(NewDepartmentName);
        if (!newDepartment.Valid)
        {
            TempData["Error"] = newDepartment.Error;
            return RedirectToPage();
        }
        var cat = new ComplaintCategory
        {
            Name            = NewName.Trim(),
            DepartmentName  = newDepartment.Name,
            DefaultPriority = NewDefaultPriority,
            SortOrder       = NewSortOrder,
            IsActive        = true
        };
        db.ComplaintCategories.Add(cat);
        await db.SaveChangesAsync();
        await auditService.LogAsync("CreateCategory", GetActorId(), GetActorCode(),
            "ComplaintCategory", cat.Id.ToString(), new { cat.Name }, GetIp());
        TempData["Success"] = $"เพิ่มหมวดหมู่ \"{cat.Name}\" เรียบร้อยแล้ว";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName))
        {
            TempData["Error"] = "กรุณากรอกชื่อหมวดหมู่";
            return RedirectToPage(new { editId = EditCatId });
        }
        var cat = await db.ComplaintCategories.FindAsync(EditCatId);
        if (cat == null) return NotFound();

        var editDepartment = await ResolveDepartmentAsync(EditDepartmentName, cat.DepartmentName);
        if (!editDepartment.Valid)
        {
            TempData["Error"] = editDepartment.Error;
            return RedirectToPage(new { editId = EditCatId });
        }

        cat.Name            = EditName.Trim();
        cat.DepartmentName  = editDepartment.Name;
        cat.DefaultPriority = EditDefaultPriority;
        cat.SortOrder       = EditSortOrder;
        cat.IsActive        = EditIsActive;
        await db.SaveChangesAsync();
        await auditService.LogAsync("EditCategory", GetActorId(), GetActorCode(),
            "ComplaintCategory", cat.Id.ToString(), new { cat.Name, cat.IsActive }, GetIp());
        TempData["Success"] = $"แก้ไขหมวดหมู่ \"{cat.Name}\" เรียบร้อยแล้ว";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var cat = await db.ComplaintCategories.FindAsync(id);
        if (cat == null) return NotFound();
        cat.IsActive = !cat.IsActive;
        await db.SaveChangesAsync();
        await auditService.LogAsync(cat.IsActive ? "ActivateCategory" : "DeactivateCategory",
            GetActorId(), GetActorCode(), "ComplaintCategory", id.ToString(), null, GetIp());
        TempData["Success"] = $"{(cat.IsActive ? "เปิดใช้งาน" : "ปิดใช้งาน")} \"{cat.Name}\" เรียบร้อย";
        return RedirectToPage();
    }

    // ══ Sub-category handlers ══

    public async Task<IActionResult> OnPostCreateSubAsync()
    {
        if (string.IsNullOrWhiteSpace(NewSubName))
        {
            TempData["Error"] = "กรุณากรอกชื่อหัวข้อย่อย";
            return RedirectToPage();
        }
        var cat = await db.ComplaintCategories.FindAsync(NewSubCategoryId);
        if (cat == null) return NotFound();

        var sub = new ComplaintSubCategory
        {
            CategoryId = NewSubCategoryId,
            Name       = NewSubName.Trim(),
            SortOrder  = NewSubSortOrder,
            IsActive   = true
        };
        db.ComplaintSubCategories.Add(sub);
        await db.SaveChangesAsync();
        await auditService.LogAsync("CreateSubCategory", GetActorId(), GetActorCode(),
            "ComplaintSubCategory", sub.Id.ToString(), new { sub.Name, ParentId = cat.Id, ParentName = cat.Name }, GetIp());
        TempData["Success"] = $"เพิ่มหัวข้อย่อย \"{sub.Name}\" ใต้หมวด \"{cat.Name}\" เรียบร้อยแล้ว";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditSubAsync()
    {
        if (string.IsNullOrWhiteSpace(EditSubName))
        {
            TempData["Error"] = "กรุณากรอกชื่อหัวข้อย่อย";
            return RedirectToPage(new { editSubId = EditSubCatId });
        }
        var sub = await db.ComplaintSubCategories.FindAsync(EditSubCatId);
        if (sub == null) return NotFound();

        sub.Name      = EditSubName.Trim();
        sub.SortOrder = EditSubSortOrder;
        sub.IsActive  = EditSubIsActive;
        await db.SaveChangesAsync();
        await auditService.LogAsync("EditSubCategory", GetActorId(), GetActorCode(),
            "ComplaintSubCategory", sub.Id.ToString(), new { sub.Name, sub.IsActive }, GetIp());
        TempData["Success"] = $"แก้ไขหัวข้อย่อย \"{sub.Name}\" เรียบร้อยแล้ว";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleSubActiveAsync(int id)
    {
        var sub = await db.ComplaintSubCategories.FindAsync(id);
        if (sub == null) return NotFound();
        sub.IsActive = !sub.IsActive;
        await db.SaveChangesAsync();
        await auditService.LogAsync(sub.IsActive ? "ActivateSubCategory" : "DeactivateSubCategory",
            GetActorId(), GetActorCode(), "ComplaintSubCategory", id.ToString(), null, GetIp());
        TempData["Success"] = $"{(sub.IsActive ? "เปิดใช้งาน" : "ปิดใช้งาน")} หัวข้อย่อย \"{sub.Name}\" เรียบร้อย";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteSubAsync(int id)
    {
        var sub = await db.ComplaintSubCategories.FindAsync(id);
        if (sub == null) return NotFound();
        var name = sub.Name;
        db.ComplaintSubCategories.Remove(sub);
        await db.SaveChangesAsync();
        await auditService.LogAsync("DeleteSubCategory", GetActorId(), GetActorCode(),
            "ComplaintSubCategory", id.ToString(), new { name }, GetIp());
        TempData["Success"] = $"ลบหัวข้อย่อย \"{name}\" เรียบร้อยแล้ว";
        return RedirectToPage();
    }

    private int    GetActorId()   => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string GetActorCode() => User.FindFirstValue("EmployeeCode") ?? "";
    private string GetIp()        => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private async Task<(bool Valid, string? Name, string? Error)> ResolveDepartmentAsync(
        string? value,
        string? currentValue = null)
    {
        var name = value?.Trim();
        if (string.IsNullOrWhiteSpace(name)) return (true, null, null);

        var exists = await db.ComplaintDepartments.AnyAsync(x =>
            x.Name == name && (x.IsActive || x.Name == currentValue));
        return exists
            ? (true, name, null)
            : (false, null, "ฝ่ายที่เลือกไม่พร้อมใช้งาน กรุณาเลือกใหม่");
    }

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
