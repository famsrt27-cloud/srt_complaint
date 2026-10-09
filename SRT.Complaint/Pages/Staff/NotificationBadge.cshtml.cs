#nullable enable
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRT.Complaint.Services;

namespace SRT.Complaint.Pages.Staff;

[Authorize(Policy = "StaffOnly")]
public class NotificationBadgeModel(IComplaintService complaintService, ICorruptionService corruptionService) : PageModel
{
    public NotificationBellVm Bell { get; private set; } = new(0, "/complaint/Staff/Queue");

    public async Task OnGetAsync()
    {
        Bell = await BuildAsync(User, complaintService, corruptionService);
    }

    public static async Task<NotificationBellVm> BuildAsync(
        ClaimsPrincipal user, IComplaintService complaintService, ICorruptionService corruptionService)
    {
        var role = user.FindFirstValue(ClaimTypes.Role) ?? "";

        var generalPending = role is "GeneralOfficer" or "SuperAdmin"
            ? await complaintService.GetTotalCountAsync(new ComplaintQueueFilter(Status: "Pending"))
            : 0;

        var corruptionPending = role is "CorruptionOfficer" or "SuperAdmin"
            ? await corruptionService.GetTotalCountAsync(new CorruptionQueueFilter(Status: "Pending"))
            : 0;

        var link = role switch
        {
            "GeneralOfficer"    => "/complaint/Staff/Queue?Status=Pending",
            "CorruptionOfficer" => "/complaint/Corruption/Queue?Status=Pending",
            _                   => "/complaint/Admin/Dashboard"
        };

        return new NotificationBellVm(generalPending + corruptionPending, link);
    }
}

public record NotificationBellVm(int Count, string LinkUrl);
