namespace SRT.Complaint.Models;

public class ComplaintAcceptanceEvidence
{
    public long Id { get; set; }
    public Guid EvidenceId { get; set; } = Guid.NewGuid();
    public int ComplaintId { get; set; }
    public string AcceptanceType { get; set; } = "ComplaintTerms";
    public string TermsTitle { get; set; } = string.Empty;
    public string TermsVersion { get; set; } = string.Empty;
    public string TermsContentHash { get; set; } = string.Empty;
    public string TermsContentSnapshot { get; set; } = string.Empty;
    public string Source { get; set; } = "PublicComplaintForm";
    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;

    public Complaint Complaint { get; set; } = null!;
}
