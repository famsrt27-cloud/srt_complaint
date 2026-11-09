using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRT.Complaint.Services;

namespace SRT.Complaint.Pages.Staff;

[Authorize(Policy = "GeneralComplaintAccess")]
public class DashboardModel(IComplaintService complaintService) : PageModel
{
    public DashboardStats Stats { get; private set; } = null!;

    public async Task OnGetAsync()
    {
        ViewData["Title"] = "แดชบอร์ด";
        Stats = await complaintService.GetDashboardStatsAsync();
    }
}
