using SRT.Complaint.Models;
using SRT.Complaint.Models.Corruption;

namespace SRT.Complaint.Services;

public interface IPdfExportService
{
    byte[] GenerateComplaintPdf(Models.Complaint complaint, string? officerName = null, bool maskReporter = false);
    byte[] GenerateCorruptionReportPdf(CorruptionReport report, string? officerName = null);
}
