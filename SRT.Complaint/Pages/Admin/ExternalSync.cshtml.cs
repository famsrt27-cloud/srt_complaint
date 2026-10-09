#nullable enable
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRT.Complaint.Models;
using SRT.Complaint.Services;

namespace SRT.Complaint.Pages.Admin;

[Authorize(Policy = "SuperAdmin")]
public class ExternalSyncModel(
    IExternalSyncService syncService,
    ITraffySettingsService traffySettingsService,
    ITraffyConnectionTester traffyConnectionTester,
    IAuditService auditService) : PageModel
{
    public IReadOnlyList<IExternalSystemAdapter> Systems { get; private set; } = [];
    public IReadOnlyList<ExternalSyncLog>         Logs   { get; private set; } = [];
    public TraffySettingsStatusVm TraffyStatus { get; private set; } = null!;

    [BindProperty(SupportsGet = true)] public string? LastResult { get; set; }

    [BindProperty] public SettingsInputModel Settings { get; set; } = new();

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "ดึงคำร้องจากระบบภายนอก";
        Systems      = syncService.GetAvailableSystems();
        Logs         = await syncService.GetRecentLogsAsync(15);
        TraffyStatus = await traffySettingsService.GetStatusAsync();

        Settings = new SettingsInputModel
        {
            ApiUrl = TraffyStatus.ApiUrl,
            Username = TraffyStatus.Username ?? "",
            OrgId = TraffyStatus.OrgId ?? ""
        };
    }

    public async Task<IActionResult> OnPostSyncAsync(string systemKey)
    {
        ViewData["Title"] = "ดึงคำร้องจากระบบภายนอก";
        var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        try
        {
            var log = await syncService.SyncAsync(systemKey, actorId);
            TempData["SyncResult"] = log.SyncStatus == "Success"
                ? $"success|{log.NewCount}|{log.DuplicateCount}|{log.FetchedCount}"
                : $"failed|{log.ErrorMessage}";
        }
        catch (Exception ex)
        {
            TempData["SyncResult"] = $"failed|{ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveSettingsAsync()
    {
        ViewData["Title"] = "ดึงคำร้องจากระบบภายนอก";

        if (string.IsNullOrWhiteSpace(Settings.Username) || string.IsNullOrWhiteSpace(Settings.OrgId))
        {
            TempData["SettingsError"] = "กรุณากรอก Username และ Org ID";
            return RedirectToPage();
        }

        var actorId   = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var actorCode = User.FindFirstValue("EmployeeCode") ?? "";
        var ip        = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        await traffySettingsService.SaveAsync(
            new TraffySettingsInput(Settings.ApiUrl, Settings.Username, Settings.Password, Settings.OrgId, Settings.WebhookSecret),
            actorId);

        // Singleton adapter caches credentials for up to 5 min — invalidate so the change takes effect immediately
        traffyConnectionTester.InvalidateSettingsCache();

        // Never log the actual secret values — only that they were touched
        await auditService.LogAsync("UpdateTraffySettings", actorId, actorCode, "TraffySettings", null, new
        {
            Settings.ApiUrl,
            Settings.Username,
            Settings.OrgId,
            PasswordChanged = !string.IsNullOrWhiteSpace(Settings.Password),
            WebhookSecretChanged = !string.IsNullOrWhiteSpace(Settings.WebhookSecret)
        }, ip);

        TempData["SettingsSuccess"] = "บันทึกการตั้งค่าเรียบร้อยแล้ว";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostTestConnectionAsync()
    {
        ViewData["Title"] = "ดึงคำร้องจากระบบภายนอก";

        var (success, message) = await traffyConnectionTester.TestConnectionAsync();
        TempData["TestResult"] = $"{(success ? "success" : "failed")}|{message}";

        return RedirectToPage();
    }

    public class SettingsInputModel
    {
        [Required(ErrorMessage = "กรุณากรอก API URL")]
        public string ApiUrl { get; set; } = "https://publicapi.traffy.in.th/exchange-api";

        [Required(ErrorMessage = "กรุณากรอก Username")]
        public string Username { get; set; } = "";

        /// <summary>Left blank on save = keep the existing encrypted value.</summary>
        public string? Password { get; set; }

        [Required(ErrorMessage = "กรุณากรอก Org ID")]
        public string OrgId { get; set; } = "";

        /// <summary>Left blank on save = keep the existing encrypted value.</summary>
        public string? WebhookSecret { get; set; }
    }
}
