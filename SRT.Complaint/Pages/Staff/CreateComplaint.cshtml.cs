#nullable enable
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Services;
using SRT.Complaint.Validation;

namespace SRT.Complaint.Pages.Staff;

[Authorize(Policy = "GeneralComplaintAccess")]
public class CreateComplaintModel(
    IComplaintService complaintService,
    AppDbContext db) : PageModel
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx"];
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private const int MaxFileCount = 10;

    [BindProperty]
    public CreateComplaintInput Input { get; set; } = new();

    [BindProperty]
    public List<IFormFile> Attachments { get; set; } = [];

    public IEnumerable<SelectListItem> CategoryOptions { get; set; } = [];
    public string SubCategoriesJson { get; private set; } = "{}";

    public static readonly IReadOnlyList<SelectListItem> ChannelOptions =
    [
        new("โทรศัพท์ (Call Center)", "Phone"),
        new("Facebook", "Facebook"),
        new("Line", "Line"),
        new("อีเมล", "Email"),
        new("ยื่นด้วยตนเอง (Walk-in)", "Walkin"),
        new("ช่องทางอื่น", "Other"),
    ];

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "สร้างเรื่องร้องเรียนใหม่";
        await LoadDataAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadDataAsync();
        await ValidateSubCategoryAsync();

        if (!ModelState.IsValid)
            return Page();

        var files = Attachments.Where(x => x.Length > 0).ToList();
        var (filesValid, fileError) = await ValidateFilesAsync(files);
        if (!filesValid)
        {
            ModelState.AddModelError(nameof(Attachments), fileError);
            return Page();
        }

        var request = new SubmitComplaintRequest(
            Input.ReporterName,
            Input.ReporterPhone,
            string.IsNullOrWhiteSpace(Input.ReporterEmail) ? null : Input.ReporterEmail,
            null,
            Input.CategoryId,
            Input.SubCategoryId > 0 ? Input.SubCategoryId : null,
            string.IsNullOrWhiteSpace(Input.SubjectStation) ? null : Input.SubjectStation,
            Input.IncidentDate.HasValue ? DateOnly.FromDateTime(Input.IncidentDate.Value) : null,
            Input.Description,
            files,
            Input.Channel
        );

        var complaint = await complaintService.SubmitAsync(request);

        var staffId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await complaintService.ClaimAsync(complaint.Id, staffId);

        TempData["Success"] = $"สร้างเรื่องร้องเรียนสำเร็จ เลขที่อ้างอิง: {complaint.ReferenceNumber}";
        return RedirectToPage("/Staff/CaseDetail", new { id = complaint.Id });
    }

    private async Task LoadDataAsync()
    {
        CategoryOptions = await db.ComplaintCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString()))
            .ToListAsync();

        var subs = await db.ComplaintSubCategories
            .Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder)
            .Select(s => new { s.CategoryId, s.Id, s.Name })
            .ToListAsync();

        var grouped = subs.GroupBy(s => s.CategoryId)
            .ToDictionary(g => g.Key, g => g.Select(s => new { s.Id, s.Name }).ToList());

        SubCategoriesJson = JsonSerializer.Serialize(grouped);
    }

    private async Task ValidateSubCategoryAsync()
    {
        if (Input.CategoryId <= 0)
            return;

        var categoryExists = await db.ComplaintCategories
            .AnyAsync(x => x.IsActive && x.Id == Input.CategoryId);
        if (!categoryExists)
        {
            ModelState.AddModelError("Input.CategoryId", "ประเภทเรื่องที่เลือกไม่พร้อมใช้งาน");
            return;
        }

        var availableIds = await db.ComplaintSubCategories
            .Where(x => x.IsActive && x.CategoryId == Input.CategoryId)
            .Select(x => x.Id)
            .ToListAsync();

        if (availableIds.Count > 0 && !Input.SubCategoryId.HasValue)
        {
            ModelState.AddModelError("Input.SubCategoryId", "กรุณาเลือกหัวข้อย่อย");
            return;
        }

        if (Input.SubCategoryId.HasValue && !availableIds.Contains(Input.SubCategoryId.Value))
            ModelState.AddModelError("Input.SubCategoryId", "หัวข้อย่อยไม่สอดคล้องกับประเภทเรื่องที่เลือก");
    }

    private static async Task<(bool Valid, string Error)> ValidateFilesAsync(IReadOnlyList<IFormFile> files)
    {
        if (files.Count > MaxFileCount)
            return (false, $"แนบไฟล์ได้ไม่เกิน {MaxFileCount} ไฟล์");

        foreach (var file in files)
        {
            if (file.Length > MaxFileSizeBytes)
                return (false, $"ไฟล์ {file.FileName} มีขนาดเกิน 10 MB");
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                return (false, $"ไฟล์ {file.FileName} ไม่ใช่ชนิดที่รองรับ");
            if (!await FileSignatureValidator.HasValidSignatureAsync(file, extension))
                return (false, $"เนื้อหาไฟล์ {file.FileName} ไม่ตรงกับนามสกุลไฟล์");
        }

        return (true, string.Empty);
    }
}

public class CreateComplaintInput
{
    [Required(ErrorMessage = "กรุณาเลือกช่องทาง")]
    [RegularExpression("^(Phone|Facebook|Line|Email|Walkin|Other)$", ErrorMessage = "ช่องทางรับเรื่องไม่ถูกต้อง")]
    public string Channel { get; set; } = "Phone";

    [Required(ErrorMessage = "กรุณากรอกชื่อ-นามสกุล")]
    [StringLength(200, ErrorMessage = "ชื่อ-นามสกุลต้องไม่เกิน 200 ตัวอักษร")]
    public string ReporterName { get; set; } = string.Empty;

    private string _reporterPhone = string.Empty;

    [Required(ErrorMessage = "กรุณากรอกเบอร์โทรศัพท์")]
    [RegularExpression(@"^0[0-9]{8,9}$", ErrorMessage = "รูปแบบเบอร์โทรไม่ถูกต้อง (ตัวอย่าง: 081-234-5678 หรือ 02-123-4567)")]
    public string ReporterPhone
    {
        get => _reporterPhone;
        set => _reporterPhone = (value ?? string.Empty).Replace("-", "").Replace(" ", "");
    }

    [StringLength(200, ErrorMessage = "อีเมลต้องไม่เกิน 200 ตัวอักษร")]
    [EmailAddress(ErrorMessage = "รูปแบบอีเมลไม่ถูกต้อง")]
    public string? ReporterEmail { get; set; }

    [Required(ErrorMessage = "กรุณาเลือกประเภทเรื่อง")]
    [Range(1, int.MaxValue, ErrorMessage = "กรุณาเลือกประเภทเรื่อง")]
    public int CategoryId { get; set; }

    public int? SubCategoryId { get; set; }

    [Required(ErrorMessage = "กรุณากรอกสถานที่ / สถานีที่เกิดเหตุ")]
    [StringLength(200, ErrorMessage = "สถานที่ / สถานีต้องไม่เกิน 200 ตัวอักษร")]
    public string SubjectStation { get; set; } = string.Empty;

    [Required(ErrorMessage = "กรุณาระบุวันที่เกิดเหตุ")]
    public DateTime? IncidentDate { get; set; }

    [Required(ErrorMessage = "กรุณากรอกรายละเอียดคำร้อง")]
    [MinLength(20, ErrorMessage = "รายละเอียดต้องมีอย่างน้อย 20 ตัวอักษร")]
    [StringLength(4000, ErrorMessage = "รายละเอียดต้องไม่เกิน 4,000 ตัวอักษร")]
    public string Description { get; set; } = string.Empty;
}
