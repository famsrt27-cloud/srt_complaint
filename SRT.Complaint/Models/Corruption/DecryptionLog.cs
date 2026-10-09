#nullable enable
namespace SRT.Complaint.Models.Corruption;

public class DecryptionLog
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public int RequestedById { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }

    public CorruptionReport Report { get; set; } = null!;
    public StaffUser RequestedBy { get; set; } = null!;
}
