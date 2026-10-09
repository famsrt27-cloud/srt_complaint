using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Models;
using SRT.Complaint.Services;
using SRT.Complaint.Validation;
using Microsoft.Extensions.Configuration;

namespace SRT.Complaint.Pages.Public;

public class SubmitModel(
    IComplaintService complaintService,
    AppDbContext db,
    ITermsService termsService,
    ITurnstileService turnstile,
    IConfiguration config) : PageModel
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx"];
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const int MaxFileCount = 5;

    public string TurnstileSiteKey => config["Turnstile:SiteKey"] ?? "";

    [BindProperty]
    public ComplaintSubmitViewModel Input { get; set; } = new();

    public IEnumerable<SelectListItem> CategoryOptions { get; set; } = [];
    public string SubCategoriesJson { get; private set; } = "{}";
    public ComplaintTerms? Terms { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadCategoriesAsync();
        Terms = await termsService.GetTermsAsync();
    }

    [EnableRateLimiting("SubmitPolicy")]
    public async Task<IActionResult> OnPostAsync()
    {
        await LoadCategoriesAsync();

        // Verify Turnstile token
        var token = Request.Form["cf-turnstile-response"].ToString();
        var ip    = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (!await turnstile.VerifyAsync(token, ip))
        {
            ModelState.AddModelError("", "การยืนยันตัวตน (Turnstile) ล้มเหลว กรุณาลองใหม่อีกครั้ง");
            Terms = await termsService.GetTermsAsync();
            return Page();
        }

        var (filesValid, fileError) = await ValidateFilesAsync();
        if (!filesValid)
        {
            ModelState.AddModelError("Input.Attachments", fileError);
        }

        if (!ModelState.IsValid)
            return Page();

        var request = new SubmitComplaintRequest(
            Input.ReporterName,
            Input.ReporterPhone,
            string.IsNullOrWhiteSpace(Input.ReporterEmail) ? null : Input.ReporterEmail,
            string.IsNullOrWhiteSpace(Input.ReporterIdCard) ? null : Input.ReporterIdCard,
            Input.CategoryId,
            Input.SubCategoryId > 0 ? Input.SubCategoryId : null,
            string.IsNullOrWhiteSpace(Input.SubjectStation) ? null : Input.SubjectStation,
            Input.IncidentDate.HasValue ? DateOnly.FromDateTime(Input.IncidentDate.Value) : null,
            Input.Description,
            Input.Attachments ?? []
        );

        var complaint = await complaintService.SubmitAsync(request);

        TempData["Success"] = $"ยื่นเรื่องร้องเรียนสำเร็จ เลขที่อ้างอิง: {complaint.ReferenceNumber}";
        return RedirectToPage("/Public/Track", new { Ref = complaint.ReferenceNumber });
    }

    private async Task LoadCategoriesAsync()
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

        var grouped = subs
            .GroupBy(s => s.CategoryId)
            .ToDictionary(
                g => g.Key.ToString(),
                g => g.Select(s => new { id = s.Id, name = s.Name }).ToList()
            );

        SubCategoriesJson = JsonSerializer.Serialize(grouped);
    }

    private async Task<(bool Valid, string Error)> ValidateFilesAsync()
    {
        var files = Input.Attachments;

        if (files == null || files.Count == 0)
            return (true, string.Empty);

        if (files.Count > MaxFileCount)
            return (false, $"แนบไฟล์ได้ไม่เกิน {MaxFileCount} ไฟล์");

        foreach (var file in files)
        {
            if (file.Length > MaxFileSizeBytes)
                return (false, $"ไฟล์ \"{file.FileName}\" มีขนาดเกิน 10 MB");

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return (false, $"ไฟล์ \"{file.FileName}\" ไม่รองรับประเภทไฟล์นี้ (รองรับ: JPG, PNG, PDF, DOC, DOCX)");

            if (!await FileSignatureValidator.HasValidSignatureAsync(file, ext))
                return (false, $"ไฟล์ \"{file.FileName}\" มีเนื้อหาไม่ตรงกับนามสกุลไฟล์");
        }

        return (true, string.Empty);
    }
}

public class ComplaintSubmitViewModel
{
    [Required(ErrorMessage = "กรุณากรอกชื่อ-นามสกุล")]
    [StringLength(200, ErrorMessage = "ชื่อ-นามสกุลต้องไม่เกิน 200 ตัวอักษร")]
    [Display(Name = "ชื่อ-นามสกุล")]
    public string ReporterName { get; set; } = string.Empty;

    private string _reporterPhone = string.Empty;
    [Required(ErrorMessage = "กรุณากรอกเบอร์โทรศัพท์")]
    [RegularExpression(@"^0[0-9]{8,9}$", ErrorMessage = "รูปแบบเบอร์โทรไม่ถูกต้อง (ตัวอย่าง: 081-234-5678 หรือ 02-123-4567)")]
    [Display(Name = "เบอร์โทรศัพท์")]
    public string ReporterPhone
    {
        get => _reporterPhone;
        set => _reporterPhone = (value ?? string.Empty).Replace("-", "").Replace(" ", "");
    }

    [EmailAddress(ErrorMessage = "รูปแบบอีเมลไม่ถูกต้อง")]
    [StringLength(200)]
    [Display(Name = "อีเมล")]
    public string? ReporterEmail { get; set; }

    private string? _reporterIdCard;
    [ThaiId]
    [Display(Name = "เลขบัตรประชาชน")]
    public string? ReporterIdCard
    {
        get => _reporterIdCard;
        set => _reporterIdCard = value?.Replace("-", "").Replace(" ", "");
    }

    [Required(ErrorMessage = "กรุณาเลือกประเภทเรื่อง")]
    [Display(Name = "ประเภทเรื่อง")]
    public int CategoryId { get; set; }

    [Display(Name = "หัวข้อย่อย")]
    public int? SubCategoryId { get; set; }

    [StringLength(200, ErrorMessage = "ชื่อสถานีต้องไม่เกิน 200 ตัวอักษร")]
    [Display(Name = "สถานี / จุดที่เกิดเหตุ")]
    public string? SubjectStation { get; set; }

    [Display(Name = "วันที่เกิดเหตุ")]
    [DataType(DataType.Date)]
    public DateTime? IncidentDate { get; set; }

    [Required(ErrorMessage = "กรุณากรอกรายละเอียดเรื่องร้องเรียน")]
    [MinLength(20, ErrorMessage = "กรุณากรอกรายละเอียดอย่างน้อย 20 ตัวอักษร")]
    [StringLength(4000, ErrorMessage = "รายละเอียดต้องไม่เกิน 4,000 ตัวอักษร")]
    [Display(Name = "รายละเอียดเรื่องร้องเรียน")]
    public string Description { get; set; } = string.Empty;

    public List<IFormFile>? Attachments { get; set; }
}
