using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SRT.Complaint.Pages.Staff;

[Authorize(Policy = "StaffOnly")]
public class AccessDeniedModel : PageModel
{
    public string HomeUrl { get; private set; } = "/Staff/Dashboard";

    public void OnGet()
    {
        HomeUrl = User.FindFirstValue(ClaimTypes.Role) switch
        {
            "CorruptionOfficer" => "/Corruption/Dashboard",
            "SuperAdmin" => "/Admin/Dashboard",
            _ => "/Staff/Dashboard"
        };
    }
}
