#nullable enable
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRT.Complaint.Models;
using SRT.Complaint.Services;

namespace SRT.Complaint.Pages.Admin;

[Authorize(Policy = "SuperAdmin")]
public class ExternalSyncModel(IExternalSyncService syncService) : PageModel
{
    public IReadOnlyList<IExternalSystemAdapter> Systems { get; private set; } = [];
    public IReadOnlyList<ExternalSyncLog>         Logs   { get; private set; } = [];

    [BindProperty(SupportsGet = true)] public string? LastResult { get; set; }

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "ดึงคำร้องจากระบบภายนอก";
        Systems      = syncService.GetAvailableSystems();
        Logs         = await syncService.GetRecentLogsAsync(15);
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
}
