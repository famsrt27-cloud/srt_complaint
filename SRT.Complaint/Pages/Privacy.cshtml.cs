using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SRT.Complaint.Pages;

public class PrivacyModel(IWebHostEnvironment environment) : PageModel
{
    public const string CookieNoticeName = "SRT.CookieNotice";
    public const string CookieNoticeVersion = "2026-10-30-v1";

    public IActionResult OnPostAcknowledgeCookieNotice(string? returnUrl)
    {
        Response.Cookies.Append(CookieNoticeName, CookieNoticeVersion, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment() || Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            MaxAge = TimeSpan.FromDays(180)
        });

        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToPage("/Index");
    }

    public IActionResult OnPostResetCookieNotice()
    {
        Response.Cookies.Delete(CookieNoticeName);
        Response.Cookies.Delete("SRT.OptionalCookieConsent");
        TempData["Success"] = "ลบการตั้งค่าคุกกี้แล้ว";
        return RedirectToPage();
    }
}
