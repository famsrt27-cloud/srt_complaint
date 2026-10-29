using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRT.Complaint.Models;
using SRT.Complaint.Services;
using SRT.Complaint.Validation;

namespace SRT.Complaint.Pages.Admin;

[Authorize(Policy = "SuperAdmin")]
public class PdfSettingsModel(IPdfSignerSettingsService signerSettings) : PageModel
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png"];
    private const long MaxSignatureSizeBytes = 5 * 1024 * 1024;

    public PdfSignerSettings? Current { get; private set; }
    public bool HasSignature { get; private set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnGetSignatureAsync()
    {
        var profile = await signerSettings.GetProfileAsync();
        if (profile.SignatureImage is null || profile.SignatureContentType is null) return NotFound();
        return File(profile.SignatureImage, profile.SignatureContentType);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input.Signature is { Length: > 0 })
        {
            if (Input.Signature.Length > MaxSignatureSizeBytes)
                ModelState.AddModelError("Input.Signature", "ไฟล์ลายเซ็นต้องมีขนาดไม่เกิน 5 MB");

            var extension = Path.GetExtension(Input.Signature.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
                ModelState.AddModelError("Input.Signature", "รองรับเฉพาะไฟล์ JPG หรือ PNG");
            else if (!await FileSignatureValidator.HasValidSignatureAsync(Input.Signature, extension))
                ModelState.AddModelError("Input.Signature", "เนื้อหาไฟล์ไม่ตรงกับนามสกุลไฟล์");
        }

        if (!ModelState.IsValid)
        {
            Current = await signerSettings.GetAsync();
            HasSignature = (await signerSettings.GetProfileAsync()).SignatureImage is not null;
            return Page();
        }

        var userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
        await signerSettings.SaveAsync(
            Input.FullName,
            Input.PositionTitle,
            Input.ActingTitle,
            Input.Signature,
            userId);

        TempData["Success"] = "บันทึก Master ผู้ลงนาม PDF เรียบร้อยแล้ว";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Current = await signerSettings.GetAsync();
        var profile = await signerSettings.GetProfileAsync();
        HasSignature = profile.SignatureImage is not null;
        Input = new InputModel
        {
            FullName = Current.FullName,
            PositionTitle = Current.PositionTitle,
            ActingTitle = Current.ActingTitle
        };
    }

    public class InputModel
    {
        [Required(ErrorMessage = "กรุณาระบุชื่อผู้ลงนาม")]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "กรุณาระบุตำแหน่ง")]
        [MaxLength(200)]
        public string PositionTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "กรุณาระบุข้อความปฏิบัติการแทน")]
        [MaxLength(300)]
        public string ActingTitle { get; set; } = string.Empty;

        public IFormFile? Signature { get; set; }
    }
}
