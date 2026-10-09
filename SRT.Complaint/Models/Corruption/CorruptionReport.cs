#nullable enable
namespace SRT.Complaint.Models.Corruption;

public class CorruptionReport
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;

    public byte[] ReporterNameEncrypted { get; set; } = Array.Empty<byte>();
    public byte[] ReporterPhoneEncrypted { get; set; } = Array.Empty<byte>();
    public byte[]? ReporterEmailEncrypted { get; set; }
    public byte[] ReporterIdCardEncrypted { get; set; } = Array.Empty<byte>();

    public string ReporterNameMasked { get; set; } = string.Empty;
    public string ReporterPhoneMasked { get; set; } = string.Empty;
    public string? ReporterEmailMasked { get; set; }

    public string SubjectType { get; set; } = string.Empty;
    public string? SubjectPersonName { get; set; }
    public string? SubjectDepartment { get; set; }
    public DateOnly? IncidentDate { get; set; }
    public string Description { get; set; } = string.Empty;

    public string Priority { get; set; } = "Normal";
    public string Status { get; set; } = "Pending";
    public int? AssignedToId { get; set; }
    public DateTime? AssignedAt { get; set; }

    public DateTime? SlaDeadline { get; set; }
    public bool SlaBreached { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public ICollection<InvestigationLog> InvestigationLogs { get; set; } = new List<InvestigationLog>();
    public ICollection<DecryptionLog> DecryptionLogs { get; set; } = new List<DecryptionLog>();
}
