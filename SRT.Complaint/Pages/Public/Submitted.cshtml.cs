#nullable enable
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QRCoder;
using SRT.Complaint.Services;

namespace SRT.Complaint.Pages.Public;

public class SubmittedModel(IComplaintService complaintService) : PageModel
{
    public string ReferenceNumber { get; private set; } = string.Empty;
    public string TrackingUrl { get; private set; } = string.Empty;
    public string QrCodeSvg { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string? @ref)
    {
        var reference = (@ref ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(reference))
            return RedirectToPage("/Public/Submit");

        var complaint = await complaintService.GetByReferenceAsync(reference);
        if (complaint is null)
            return RedirectToPage("/Public/Submit");

        ReferenceNumber = complaint.ReferenceNumber;
        TrackingUrl = Url.Page(
            "/Public/Track",
            pageHandler: null,
            values: new { @ref = ReferenceNumber },
            protocol: Request.Scheme) ?? string.Empty;

        using var qrCodeData = QRCodeGenerator.GenerateQrCode(TrackingUrl, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new SvgQRCode(qrCodeData);
        QrCodeSvg = qrCode.GetGraphic(
            pixelsPerModule: 8,
            darkColorHex: "#0D2E6E",
            lightColorHex: "#FFFFFF",
            drawQuietZones: true);
        ViewData["Title"] = "ยื่นเรื่องร้องเรียนสำเร็จ";
        return Page();
    }
}
