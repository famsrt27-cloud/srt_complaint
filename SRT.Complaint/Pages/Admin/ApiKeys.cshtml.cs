#nullable enable
using System.ComponentModel.DataAnnotations;
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
public class ApiKeysModel(
    AppDbContext db,
    IApiKeyService apiKeyService,
    IAuditService auditService,
    IConfiguration configuration) : PageModel
{
    public string? RuntimeEnvironment { get; } =
        ApiIntegrationPolicy.AllowedEnvironments.Contains(configuration["ApiManagement:Environment"] ?? "")
            ? configuration["ApiManagement:Environment"]
            : null;
    public IReadOnlyList<ApiKey> Keys { get; private set; } = [];
    public IReadOnlyList<Partner> Partners { get; private set; } = [];
    public string? NewRawKey { get; private set; }
    public string? NewRawKeyName { get; private set; }

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? EnvironmentFilter { get; set; }

    [BindProperty, Range(1, int.MaxValue)] public int NewPartnerId { get; set; }
    [BindProperty, Required, StringLength(200)] public string? NewName { get; set; }
    [BindProperty] public string NewKeyType { get; set; } = "External";
    [BindProperty] public string NewEnvironment { get; set; } = "Sandbox";
    [BindProperty] public string NewDataAccessMode { get; set; } = "Own";
    [BindProperty, StringLength(500)] public string? NewPurpose { get; set; }
    [BindProperty] public List<string> NewScopes { get; set; } = [];
    [BindProperty, Range(1, 3600)] public int NewRateLimit { get; set; } = 60;
    [BindProperty] public string? NewAllowedIps { get; set; }
    [BindProperty, Required] public string? NewExpiresAt { get; set; } = ApiKeyExpirationPolicy.GetInputDate(180);

    [BindProperty, StringLength(50)] public string? PartnerCode { get; set; }
    [BindProperty, StringLength(200)] public string? PartnerName { get; set; }
    [BindProperty, StringLength(200)] public string? PartnerContactName { get; set; }
    [BindProperty, EmailAddress, StringLength(200)] public string? PartnerContactEmail { get; set; }
    [BindProperty, StringLength(50)] public string? PartnerContactPhone { get; set; }

    [BindProperty] public int RevokeId { get; set; }
    [BindProperty, StringLength(500)] public string? RevokeReason { get; set; }
    [BindProperty] public int RotateId { get; set; }

    public static readonly ScopeOption[] AvailableScopes =
    [
        new("metadata:read", "อ่านข้อมูลอ้างอิง", "หมวดหมู่ สถานะ และรายการ Event", "basic"),
        new("complaints:write", "ส่งเรื่องร้องเรียน", "สร้างเรื่องใหม่ในนาม Partner", "submit"),
        new("complaints:status", "ติดตามสถานะ", "อ่านสถานะเรื่องของ Partner", "track"),
        new("complaints:read", "อ่านรายละเอียดเรื่อง", "รวมข้อมูลผู้ร้องและรายละเอียดเรื่อง", "read"),
        new("complaints:update", "อัปเดตสถานะ", "เปลี่ยนสถานะและเพิ่มข้อความตอบกลับ", "update"),
        new("complaints:edoc", "รับข้อมูล eDOC", "อ่าน payload สำหรับระบบสารบรรณ", "edoc"),
        new("stats:summary", "สถิติสรุป", "อ่านสถิติตามขอบเขตข้อมูลของคีย์", "stats"),
        new("stats:detailed", "สถิติรายละเอียด", "อ่านสถิติแยกหมวด สถานะ และความเร่งด่วน", "stats"),
        new("webhooks:manage", "จัดการ Webhook", "ลงทะเบียนและปิด Webhook ของคีย์นี้", "webhook"),
        new("corruption:stats", "สถิติทุจริต", "ข้อมูลภายในระดับสูง อนุญาตเฉพาะ Internal", "sensitive")
    ];

    public int ActiveCount => Keys.Count(x => GetStatus(x) is "active" or "expiring");
    public int ExpiringCount => Keys.Count(x => GetStatus(x) == "expiring");
    public int NeverUsedCount => Keys.Count(x => x.LastUsedAt == null && GetStatus(x) is "active" or "expiring");
    public int PartnerCount => Partners.Count(x => x.IsActive);

    public async Task OnGetAsync()
    {
        if (RuntimeEnvironment != null)
            NewEnvironment = RuntimeEnvironment;
        NewRawKey = HttpContext.Session.GetString("api-key:raw");
        NewRawKeyName = HttpContext.Session.GetString("api-key:name");
        HttpContext.Session.Remove("api-key:raw");
        HttpContext.Session.Remove("api-key:name");
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreatePartnerAsync()
    {
        ModelState.ClearValidationState(nameof(NewName));
        if (string.IsNullOrWhiteSpace(PartnerCode) || string.IsNullOrWhiteSpace(PartnerName))
        {
            TempData["Error"] = "กรุณากรอกรหัสและชื่อ Partner";
            return RedirectToPage();
        }

        var code = PartnerCode.Trim().ToUpperInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[A-Z0-9][A-Z0-9_-]{1,49}$"))
        {
            TempData["Error"] = "รหัส Partner ใช้ได้เฉพาะ A-Z, 0-9, _ และ - ความยาว 2–50 ตัวอักษร";
            return RedirectToPage();
        }
        if (await db.Partners.AnyAsync(x => x.Code == code))
        {
            TempData["Error"] = $"รหัส Partner {code} มีอยู่แล้ว";
            return RedirectToPage();
        }

        var partner = new Partner
        {
            Code = code,
            Name = PartnerName.Trim(),
            ContactName = NullIfWhiteSpace(PartnerContactName),
            ContactEmail = NullIfWhiteSpace(PartnerContactEmail),
            ContactPhone = NullIfWhiteSpace(PartnerContactPhone),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Partners.Add(partner);
        await db.SaveChangesAsync();
        await auditService.LogAsync("CreateApiPartner", GetActorId(), GetActorCode(),
            "Partner", partner.Id.ToString(), new { partner.Code, partner.Name }, GetIp());
        TempData["Success"] = $"เพิ่ม Partner {partner.Name} แล้ว";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }
        if (NewScopes.Count == 0)
        {
            ModelState.AddModelError(nameof(NewScopes), "กรุณาเลือกสิทธิ์อย่างน้อย 1 รายการ");
            await LoadAsync();
            return Page();
        }
        if (!ApiKeyExpirationPolicy.TryParseInputDate(NewExpiresAt, out var expiresAtUtc))
        {
            ModelState.AddModelError(nameof(NewExpiresAt), "รูปแบบวันหมดอายุไม่ถูกต้อง");
            await LoadAsync();
            return Page();
        }

        try
        {
            var request = new CreateApiKeyRequest(
                NewPartnerId,
                NewName!.Trim(),
                NewKeyType,
                NewEnvironment,
                NewDataAccessMode,
                NullIfWhiteSpace(NewPurpose),
                NewScopes,
                NewRateLimit,
                NewAllowedIps,
                expiresAtUtc,
                GetActorId());

            var (key, rawKey) = await apiKeyService.CreateAsync(request);
            HttpContext.Session.SetString("api-key:raw", rawKey);
            HttpContext.Session.SetString("api-key:name", key.Name);
            TempData["Success"] = $"สร้าง API Key “{key.Name}” แล้ว กรุณาคัดลอกและจัดเก็บทันที";
            return RedirectToPage();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostRotateAsync()
    {
        try
        {
            var (key, rawKey) = await apiKeyService.RotateAsync(RotateId, GetActorId(), TimeSpan.FromDays(7));
            HttpContext.Session.SetString("api-key:raw", rawKey);
            HttpContext.Session.SetString("api-key:name", key.Name);
            TempData["Success"] = "สร้างคีย์ใหม่แล้ว คีย์เดิมจะใช้ได้ต่ออีก 7 วัน";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRevokeAsync()
    {
        if (string.IsNullOrWhiteSpace(RevokeReason))
        {
            TempData["Error"] = "กรุณาระบุเหตุผลในการยกเลิก";
            return RedirectToPage();
        }

        try
        {
            await apiKeyService.RevokeAsync(RevokeId, GetActorId(), RevokeReason.Trim());
            TempData["Success"] = "ยกเลิก API Key แล้ว";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage();
    }

    public static string GetStatus(ApiKey key)
    {
        if (!key.IsActive) return "revoked";
        if (key.ExpiresAt.HasValue && key.ExpiresAt <= DateTime.UtcNow) return "expired";
        if (key.ExpiresAt.HasValue && key.ExpiresAt <= DateTime.UtcNow.AddDays(30)) return "expiring";
        return "active";
    }

    private async Task LoadAsync()
    {
        Partners = await db.Partners.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        var keys = await apiKeyService.ListAsync();
        IEnumerable<ApiKey> filtered = keys;
        if (!string.IsNullOrWhiteSpace(Search))
            filtered = filtered.Where(x => x.Name.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                                           x.Partner.Name.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                                           x.Partner.Code.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
                                           x.KeyPrefix.Contains(Search, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(Status))
            filtered = filtered.Where(x => GetStatus(x) == Status);
        if (!string.IsNullOrWhiteSpace(EnvironmentFilter))
            filtered = filtered.Where(x => x.Environment == EnvironmentFilter);
        Keys = filtered.ToList();
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private int GetActorId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string GetActorCode() => User.FindFirstValue("EmployeeCode") ?? "";
    private string GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

public record ScopeOption(string Value, string Label, string Description, string Group);
