#nullable enable
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using System.ComponentModel.DataAnnotations;
using SRT.Complaint.Models;
using SRT.Complaint.Services;
using SRT.Complaint.Validation;

namespace SRT.Complaint.Pages.Public;

public class SubmitCorruptionModel(
    ICorruptionService corruptionService,
    ITermsService termsService,
    ITurnstileService turnstile,
    IConfiguration config) : PageModel
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx"];
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;
    private const int MaxFileCount = 5;

    public string TurnstileSiteKey => config["Turnstile:SiteKey"] ?? "";

    [BindProperty] public CorruptionInputModel Input { get; set; } = new();
    public ComplaintTerms? Terms { get; private set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "แจ้งเบาะแสทุจริต";
        Terms = await termsService.GetTermsAsync();
    }

    [EnableRateLimiting("SubmitPolicy")]
    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Title"] = "แจ้งเบาะแสทุจริต";

        if (!ModelState.IsValid)
            return Page();

        // Verify Turnstile token
        var token = Request.Form["cf-turnstile-response"].ToString();
        var ip    = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (!await turnstile.VerifyAsync(token, ip))
        {
            ModelState.AddModelError("", "การยืนยันตัวตน (Turnstile) ล้มเหลว กรุณาลองใหม่อีกครั้ง");
            Terms = await termsService.GetTermsAsync();
            return Page();
        }

        var attachments = Input.Attachments?
            .Where(f => f.Length > 0)
            .ToList() ?? [];

        var (filesValid, fileError) = await ValidateFilesAsync(attachments);
        if (!filesValid)
        {
            ModelState.AddModelError("Input.Attachments", fileError);
            return Page();
        }

        var request = new SubmitCorruptionRequest(
            ReporterName:      Input.ReporterName.Trim(),
            ReporterPhone:     Input.ReporterPhone.Trim(),
            ReporterEmail:     string.IsNullOrWhiteSpace(Input.ReporterEmail) ? null : Input.ReporterEmail.Trim(),
            ReporterIdCard:    Input.ReporterIdCard.Trim(),
            SubjectType:       Input.SubjectType,
            SubjectPersonName: string.IsNullOrWhiteSpace(Input.SubjectPersonName) ? null : Input.SubjectPersonName.Trim(),
            SubjectDepartment: string.IsNullOrWhiteSpace(Input.SubjectDepartment) ? null : Input.SubjectDepartment.Trim(),
            IncidentDate:      Input.IncidentDate.HasValue ? DateOnly.FromDateTime(Input.IncidentDate.Value) : null,
            Description:       Input.Description.Trim(),
            Attachments:       attachments
        );

        var report = await corruptionService.SubmitAsync(request);

        TempData["CorruptionRef"] = report.ReferenceNumber;
        return RedirectToPage("/Public/CorruptionSubmitted");
    }

    private static async Task<(bool Valid, string Error)> ValidateFilesAsync(IList<IFormFile> files)
    {
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

public class CorruptionInputModel
{
    [Required(ErrorMessage = "กรุณากรอกชื่อ-นามสกุล")]
    [MaxLength(200)]
    public string ReporterName { get; set; } = "";

    private string _reporterPhone = "";
    [Required(ErrorMessage = "กรุณากรอกเบอร์โทรศัพท์")]
    [RegularExpression(@"^0[0-9]{8,9}$", ErrorMessage = "รูปแบบเบอร์โทรไม่ถูกต้อง (ตัวอย่าง: 081-234-5678 หรือ 02-123-4567)")]
    public string ReporterPhone
    {
        get => _reporterPhone;
        set => _reporterPhone = (value ?? "").Replace("-", "").Replace(" ", "");
    }

    [EmailAddress(ErrorMessage = "รูปแบบอีเมลไม่ถูกต้อง")]
    public string? ReporterEmail { get; set; }

    private string _reporterIdCard = "";
    [Required(ErrorMessage = "กรุณากรอกเลขบัตรประชาชน")]
    [ThaiId]
    public string ReporterIdCard
    {
        get => _reporterIdCard;
        set => _reporterIdCard = (value ?? "").Replace("-", "").Replace(" ", "");
    }

    [Required(ErrorMessage = "กรุณาเลือกประเภทผู้ถูกกล่าวหา")]
    public string SubjectType { get; set; } = "";

    [MaxLength(200)]
    public string? SubjectPersonName { get; set; }

    [MaxLength(200)]
    public string? SubjectDepartment { get; set; }

    public DateTime? IncidentDate { get; set; }

    [Required(ErrorMessage = "กรุณากรอกรายละเอียด")]
    [MinLength(20, ErrorMessage = "กรุณาระบุรายละเอียดอย่างน้อย 20 ตัวอักษร")]
    [MaxLength(4000)]
    public string Description { get; set; } = "";

    public List<IFormFile>? Attachments { get; set; }
}
