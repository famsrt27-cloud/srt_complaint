#nullable enable
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRT.Complaint.Data;
using SRT.Complaint.Validation;

namespace SRT.Complaint.Pages.Staff;

[Authorize(Policy = "StaffOnly")]
public class ChangePasswordModel(AppDbContext db) : PageModel
{
    public bool IsForced => User.HasClaim("MustChangePassword", "true");

    [BindProperty]
    [Required(ErrorMessage = "กรุณากรอกรหัสผ่านปัจจุบัน")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "กรุณากรอกรหัสผ่านใหม่")]
    [PasswordStrength]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "กรุณายืนยันรหัสผ่านใหม่")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    public IActionResult OnGet()
    {
        ViewData["Title"] = "เปลี่ยนรหัสผ่าน";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ViewData["Title"] = "เปลี่ยนรหัสผ่าน";

        if (!ModelState.IsValid)
            return Page();

        if (NewPassword != ConfirmPassword)
        {
            ModelState.AddModelError(string.Empty, "รหัสผ่านใหม่และการยืนยันไม่ตรงกัน");
            return Page();
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var staff = await db.StaffUsers.FindAsync(userId);
        if (staff == null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(CurrentPassword, staff.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "รหัสผ่านปัจจุบันไม่ถูกต้อง");
            return Page();
        }

        if (BCrypt.Net.BCrypt.Verify(NewPassword, staff.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "รหัสผ่านใหม่ต้องไม่ซ้ำกับรหัสผ่านปัจจุบัน");
            return Page();
        }

        staff.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword, workFactor: 12);
        staff.MustChangePassword = false;
        staff.TempPasswordExpiresAt = null;
        staff.TempPasswordEncrypted = null;
        staff.AuthVersion++;
        await db.SaveChangesAsync();

        // Re-issue cookie without MustChangePassword claim
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, staff.Id.ToString()),
            new(ClaimTypes.Name, staff.EmployeeCode),
            new(ClaimTypes.Role, staff.Role),
            new("AuthVersion", staff.AuthVersion.ToString()),
            new("FullName", staff.FullName),
            new("EmployeeCode", staff.EmployeeCode)
        };

        await HttpContext.SignOutAsync("Cookies");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));
        await HttpContext.SignInAsync("Cookies", principal, new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        });

        TempData["Success"] = "เปลี่ยนรหัสผ่านเรียบร้อยแล้ว";
        return staff.Role switch
        {
            "CorruptionOfficer" => RedirectToPage("/Corruption/Dashboard"),
            "SuperAdmin" => RedirectToPage("/Admin/Dashboard"),
            _ => RedirectToPage("/Staff/Dashboard")
        };
    }
}
