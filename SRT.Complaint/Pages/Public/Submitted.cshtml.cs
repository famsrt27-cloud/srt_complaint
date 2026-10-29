#nullable enable
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SRT.Complaint.Services;

namespace SRT.Complaint.Pages.Public;

public class SubmittedModel(IComplaintService complaintService) : PageModel
{
    public string ReferenceNumber { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string? @ref)
    {
        var reference = (@ref ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(reference))
            return RedirectToPage("/Public/Submit");

        var complaint = await complaintService.GetByReferenceAsync(reference);
        if (complaint is null)
            return RedirectToPage("/Public/Submit");

        ReferenceNumber = complaint.ReferenceNumber;
        ViewData["Title"] = "ยื่นเรื่องร้องเรียนสำเร็จ";
        return Page();
    }
}
