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
public class NotificationsModel(AppDbContext db, IAuditService auditService) : PageModel
{
    public IReadOnlyList<NotificationTemplate> Templates { get; private set; } = [];
    public IReadOnlyList<NotificationDeliveryLog> RecentDeliveries { get; private set; } = [];

    [BindProperty] public int     SaveId           { get; set; }
    [BindProperty] public string? SaveEmailSubject { get; set; }
    [BindProperty] public string? SaveEmailBody    { get; set; }
    [BindProperty] public string? SaveSmsBody      { get; set; }
    [BindProperty] public bool    SaveIsEmailEnabled { get; set; }
    [BindProperty] public bool    SaveIsSmsEnabled   { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "การแจ้งเตือน";
        Templates = await db.NotificationTemplates.OrderBy(t => t.Id).ToListAsync();
        RecentDeliveries = await db.NotificationDeliveryLogs.AsNoTracking()
            .OrderByDescending(x => x.AttemptedAt)
            .Take(50)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostSaveTemplateAsync()
    {
        var tmpl = await db.NotificationTemplates.FindAsync(SaveId);
        if (tmpl == null) return NotFound();

        tmpl.EmailSubject    = string.IsNullOrWhiteSpace(SaveEmailSubject) ? null : SaveEmailSubject.Trim();
        tmpl.EmailBody       = string.IsNullOrWhiteSpace(SaveEmailBody)    ? null : SaveEmailBody.Trim();
        tmpl.SmsBody         = string.IsNullOrWhiteSpace(SaveSmsBody)      ? null : SaveSmsBody.Trim();
        tmpl.IsEmailEnabled  = SaveIsEmailEnabled;
        tmpl.IsSmsEnabled    = SaveIsSmsEnabled;
        await db.SaveChangesAsync();
        await auditService.LogAsync("UpdateNotificationTemplate", GetActorId(), GetActorCode(),
            "NotificationTemplate", tmpl.Id.ToString(), new { tmpl.EventKey }, GetIp());

        TempData["Success"] = $"บันทึก Template \"{tmpl.LabelTh}\" เรียบร้อยแล้ว";
        return RedirectToPage();
    }

    private int    GetActorId()   => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string GetActorCode() => User.FindFirstValue("EmployeeCode") ?? "";
    private string GetIp()        => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
