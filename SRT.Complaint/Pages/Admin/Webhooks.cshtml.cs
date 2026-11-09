#nullable enable
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRT.Complaint.Models;
using SRT.Complaint.Services;
using SRT.Complaint.Data;
using Microsoft.EntityFrameworkCore;

namespace SRT.Complaint.Pages.Admin;

[Authorize(Policy = "SuperAdmin")]
public class WebhooksModel(IWebhookService webhookService, IAuditService auditService, AppDbContext db) : PageModel
{
    public IReadOnlyList<Webhook> Webhooks { get; private set; } = [];
    public IReadOnlyList<ApiKey> ApiKeys { get; private set; } = [];

    [BindProperty] public int DeleteId { get; set; }
    [BindProperty] public int NewApiKeyId { get; set; }
    [BindProperty] public string? NewName { get; set; }
    [BindProperty] public string? NewTargetUrl { get; set; }
    [BindProperty] public List<string> NewEvents { get; set; } = [];

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "Webhooks";
        Webhooks = await webhookService.ListAllAsync();
        ApiKeys = await db.ApiKeys.AsNoTracking().Include(x => x.Partner).Include(x => x.Scopes)
            .Where(x => x.IsActive && (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow) &&
                        x.Scopes.Any(s => s.Scope == "webhooks:manage"))
            .OrderBy(x => x.Partner.Name).ThenBy(x => x.Name).ToListAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var allowedEvents = new[] { "complaint.created", "complaint.status_changed", "complaint.closed" };
        var events = NewEvents.Where(x => allowedEvents.Contains(x)).Distinct().ToList();
        if (NewApiKeyId <= 0 || string.IsNullOrWhiteSpace(NewName) || string.IsNullOrWhiteSpace(NewTargetUrl) || events.Count == 0)
        {
            TempData["Error"] = "กรุณากรอกชื่อ URL เลือก API Key และเหตุการณ์อย่างน้อย 1 รายการ";
            return RedirectToPage();
        }

        try
        {
            var (webhook, secret) = await webhookService.CreateAsync(NewApiKeyId, NewName.Trim(), NewTargetUrl.Trim(), events);
            await auditService.LogAsync("CreateWebhook", GetActorId(), GetActorCode(),
                "Webhook", webhook.Id.ToString(), new { webhook.Name, webhook.ApiKeyId, events }, GetIp());
            TempData["Success"] = "สร้าง Webhook เรียบร้อยแล้ว";
            TempData["WebhookSecret"] = secret;
            TempData["WebhookName"] = webhook.Name;
        }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        await webhookService.DeleteAsync(DeleteId);
        await auditService.LogAsync("DeleteWebhook", GetActorId(), GetActorCode(),
            "Webhook", DeleteId.ToString(), null, GetIp());
        TempData["Success"] = "ลบ Webhook เรียบร้อยแล้ว";
        return RedirectToPage();
    }

    public static List<string> ParseEvents(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }

    private int    GetActorId()   => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private string GetActorCode() => User.FindFirstValue("EmployeeCode") ?? "";
    private string GetIp()        => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
