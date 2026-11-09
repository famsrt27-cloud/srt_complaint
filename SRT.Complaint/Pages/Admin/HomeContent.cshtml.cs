#nullable enable
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRT.Complaint.Models;
using SRT.Complaint.Services;
using SRT.Complaint.Validation;

namespace SRT.Complaint.Pages.Admin;

[Authorize(Policy = "SuperAdmin")]
public class HomeContentModel(IContentBlockService contentService, IWebHostEnvironment env) : PageModel
{
    private static readonly string[] AllowedImageExtensions = [".jpg", ".jpeg", ".png"];
    private const long MaxImageSizeBytes = 5 * 1024 * 1024;

    // camelCase so ItemsJson round-trips through the plain-JS repeater UI,
    // which reads/writes lowercase keys (item.title, item.description, ...).
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public DateTime StepsUpdatedAt { get; private set; }
    public DateTime ContactUpdatedAt { get; private set; }
    public DateTime TrustUpdatedAt { get; private set; }

    [BindProperty] public StepsInputModel StepsInput { get; set; } = new();
    [BindProperty] public ContactInputModel ContactInput { get; set; } = new();
    [BindProperty] public TrustInputModel TrustInput { get; set; } = new();

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "จัดการเนื้อหาหน้าแรก";
        var blocks = await contentService.GetHomeBlocksAsync();

        var stepsBlock = blocks.GetValueOrDefault("home_steps");
        var contactBlock = blocks.GetValueOrDefault("home_contact");
        var trustBlock = blocks.GetValueOrDefault("home_trust");

        var steps = contentService.ParseSteps(stepsBlock);
        var contact = contentService.ParseContact(contactBlock);
        var trust = contentService.ParseTrust(trustBlock);

        StepsUpdatedAt = stepsBlock?.UpdatedAt ?? default;
        ContactUpdatedAt = contactBlock?.UpdatedAt ?? default;
        TrustUpdatedAt = trustBlock?.UpdatedAt ?? default;

        StepsInput = new StepsInputModel
        {
            Title = stepsBlock?.Title ?? "ขั้นตอนการร้องเรียน",
            ItemsJson = JsonSerializer.Serialize(steps.Items, JsonOpts),
            SlaNote = steps.SlaNote,
            ImageUrl = steps.ImageUrl
        };
        ContactInput = new ContactInputModel
        {
            Title = contactBlock?.Title ?? "ช่องทางติดต่อ",
            ItemsJson = JsonSerializer.Serialize(contact.Items, JsonOpts),
            ImageUrl = contact.ImageUrl
        };
        TrustInput = new TrustInputModel
        {
            Title = trustBlock?.Title ?? "ความปลอดภัย & ความเป็นส่วนตัว",
            ItemsJson = JsonSerializer.Serialize(trust.Items, JsonOpts),
            Note = trust.Note,
            ImageUrl = trust.ImageUrl
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var stepItems = DeserializeItems<StepItemData>(StepsInput.ItemsJson)
            .Where(i => !string.IsNullOrWhiteSpace(i.Title)).ToList();
        var contactItems = DeserializeItems<ContactItemData>(ContactInput.ItemsJson)
            .Where(i => !string.IsNullOrWhiteSpace(i.Label)).ToList();
        var trustItems = DeserializeItems<TrustItemData>(TrustInput.ItemsJson)
            .Where(i => !string.IsNullOrWhiteSpace(i.Text)).ToList();

        if (stepItems.Count == 0) ModelState.AddModelError(nameof(StepsInput), "กรุณาเพิ่มอย่างน้อย 1 ขั้นตอน");
        if (contactItems.Count == 0) ModelState.AddModelError(nameof(ContactInput), "กรุณาเพิ่มอย่างน้อย 1 ช่องทางติดต่อ");
        if (trustItems.Count == 0) ModelState.AddModelError(nameof(TrustInput), "กรุณาเพิ่มอย่างน้อย 1 ข้อความ");

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "จัดการเนื้อหาหน้าแรก";
            await LoadUpdatedAtAsync();
            return Page();
        }

        var staffId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        await contentService.SaveStepsAsync(
            StepsInput.Title,
            new StepsBlockData(stepItems, StepsInput.SlaNote, NullIfEmpty(StepsInput.ImageUrl)),
            staffId);
        await contentService.SaveContactAsync(
            ContactInput.Title,
            new ContactBlockData(contactItems, NullIfEmpty(ContactInput.ImageUrl)),
            staffId);
        await contentService.SaveTrustAsync(
            TrustInput.Title,
            new TrustBlockData(trustItems, TrustInput.Note, NullIfEmpty(TrustInput.ImageUrl)),
            staffId);

        TempData["Success"] = "บันทึกเนื้อหาหน้าแรกเรียบร้อยแล้ว";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUploadImageAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "กรุณาเลือกไฟล์รูปภาพ" });

        if (file.Length > MaxImageSizeBytes)
            return BadRequest(new { error = "ไฟล์มีขนาดเกิน 5 MB" });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
            return BadRequest(new { error = "รองรับเฉพาะไฟล์ JPG หรือ PNG" });

        if (!await FileSignatureValidator.HasValidSignatureAsync(file, ext))
            return BadRequest(new { error = "ไฟล์มีเนื้อหาไม่ตรงกับนามสกุลไฟล์" });

        var uploadsDir = Path.Combine(env.WebRootPath, "uploads", "home-content");
        Directory.CreateDirectory(uploadsDir);

        var storedName = $"{Guid.NewGuid()}{ext}";
        await using (var fs = System.IO.File.Create(Path.Combine(uploadsDir, storedName)))
        {
            await file.CopyToAsync(fs);
        }

        return new JsonResult(new { url = Url.Content($"~/uploads/home-content/{storedName}") });
    }

    private static List<T> DeserializeItems<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, JsonOpts) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private async Task LoadUpdatedAtAsync()
    {
        var blocks = await contentService.GetHomeBlocksAsync();
        StepsUpdatedAt = blocks.GetValueOrDefault("home_steps")?.UpdatedAt ?? default;
        ContactUpdatedAt = blocks.GetValueOrDefault("home_contact")?.UpdatedAt ?? default;
        TrustUpdatedAt = blocks.GetValueOrDefault("home_trust")?.UpdatedAt ?? default;
    }

    public class StepsInputModel
    {
        [Required(ErrorMessage = "กรุณาระบุหัวข้อ")]
        [MaxLength(200, ErrorMessage = "หัวข้อยาวเกินไป")]
        public string Title { get; set; } = "";

        public string ItemsJson { get; set; } = "[]";

        [MaxLength(300, ErrorMessage = "ข้อความยาวเกินไป")]
        public string SlaNote { get; set; } = "";

        [MaxLength(500, ErrorMessage = "ลิงก์ยาวเกินไป")]
        public string? ImageUrl { get; set; }
    }

    public class ContactInputModel
    {
        [Required(ErrorMessage = "กรุณาระบุหัวข้อ")]
        [MaxLength(200, ErrorMessage = "หัวข้อยาวเกินไป")]
        public string Title { get; set; } = "";

        public string ItemsJson { get; set; } = "[]";

        [MaxLength(500, ErrorMessage = "ลิงก์ยาวเกินไป")]
        public string? ImageUrl { get; set; }
    }

    public class TrustInputModel
    {
        [Required(ErrorMessage = "กรุณาระบุหัวข้อ")]
        [MaxLength(200, ErrorMessage = "หัวข้อยาวเกินไป")]
        public string Title { get; set; } = "";

        public string ItemsJson { get; set; } = "[]";

        [MaxLength(500, ErrorMessage = "ข้อความยาวเกินไป")]
        public string Note { get; set; } = "";

        [MaxLength(500, ErrorMessage = "ลิงก์ยาวเกินไป")]
        public string? ImageUrl { get; set; }
    }
}
