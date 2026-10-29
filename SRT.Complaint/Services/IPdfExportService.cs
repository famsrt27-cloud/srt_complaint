using SRT.Complaint.Models;
using SRT.Complaint.Models.Corruption;

namespace SRT.Complaint.Services;

public interface IPdfExportService
{
    byte[] GenerateComplaintPdf(Models.Complaint complaint, string? officerName = null,
        bool maskReporter = false, PdfSignerProfile? signer = null,
        string? recipientDepartment = null);
    byte[] GenerateComplaintReportPdf(IReadOnlyList<Models.Complaint> complaints, string? exportedBy = null);
    byte[] GenerateCorruptionReportPdf(CorruptionReport report, string? officerName = null);
}
