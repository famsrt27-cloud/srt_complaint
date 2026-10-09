namespace SRT.Complaint.Models.Corruption;

public class InvestigationLog
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public int AuthorId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsConfidential { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public CorruptionReport Report { get; set; } = null!;
    public StaffUser Author { get; set; } = null!;
}
