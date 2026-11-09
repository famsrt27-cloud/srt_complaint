#nullable enable
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Services;

namespace SRT.Complaint.Pages.Staff;

public class LoginModel(AppDbContext db, ITurnstileService turnstile, IConfiguration config) : PageModel
{
    [BindProperty]
    public LoginInputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public string TurnstileSiteKey => config["Turnstile:SiteKey"] ?? "";

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToRoleHome(User.FindFirstValue(ClaimTypes.Role));

        ViewData["ReturnUrl"] = returnUrl;
        return Page();
    }

    [EnableRateLimiting("LoginPolicy")]
    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid)
            return Page();

        // Verify Turnstile token
        var token = Request.Form["cf-turnstile-response"].ToString();
        var ip    = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (!await turnstile.VerifyAsync(token, ip))
        {
            ErrorMessage = "การยืนยันตัวตน (Turnstile) ล้มเหลว กรุณาลองใหม่อีกครั้ง";
            return Page();
        }

        var staff = await db.StaffUsers
            .FirstOrDefaultAsync(u => u.EmployeeCode == Input.EmployeeCode && u.IsActive);

        if (staff == null || !BCrypt.Net.BCrypt.Verify(Input.Password, staff.PasswordHash))
        {
            ErrorMessage = "รหัสพนักงานหรือรหัสผ่านไม่ถูกต้อง";
            return Page();
        }

        if (staff.MustChangePassword &&
            staff.TempPasswordExpiresAt.HasValue &&
            staff.TempPasswordExpiresAt.Value < DateTime.UtcNow)
        {
            ErrorMessage = "รหัสผ่านชั่วคราวหมดอายุแล้ว กรุณาติดต่อผู้ดูแลระบบเพื่อรีเซ็ตรหัสผ่านใหม่";
            return Page();
        }

        staff.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, staff.Id.ToString()),
            new(ClaimTypes.Name, staff.EmployeeCode),
            new(ClaimTypes.Role, staff.Role),
            new("AuthVersion", staff.AuthVersion.ToString()),
            new("FullName", staff.FullName),
            new("EmployeeCode", staff.EmployeeCode)
        };

        if (staff.MustChangePassword)
            claims.Add(new Claim("MustChangePassword", "true"));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));

        await HttpContext.SignInAsync("Cookies", principal, new AuthenticationProperties
        {
            IsPersistent = Input.RememberMe,
            ExpiresUtc = Input.RememberMe
                ? DateTimeOffset.UtcNow.AddDays(7)
                : DateTimeOffset.UtcNow.AddHours(8)
        });

        if (staff.MustChangePassword)
            return RedirectToPage("/Staff/ChangePassword");

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToRoleHome(staff.Role);
    }

    private IActionResult RedirectToRoleHome(string? role) => role switch
    {
        "CorruptionOfficer" => RedirectToPage("/Corruption/Dashboard"),
        "SuperAdmin" => RedirectToPage("/Admin/Dashboard"),
        _ => RedirectToPage("/Staff/Dashboard")
    };
}

public class LoginInputModel
{
    [Required(ErrorMessage = "กรุณากรอกรหัสพนักงาน")]
    [StringLength(7, MinimumLength = 7, ErrorMessage = "รหัสพนักงาน 7 หลัก")]
    [Display(Name = "รหัสพนักงาน")]
    public string EmployeeCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "กรุณากรอกรหัสผ่าน")]
    [DataType(DataType.Password)]
    [Display(Name = "รหัสผ่าน")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "จดจำฉันไว้")]
    public bool RememberMe { get; set; }
}
