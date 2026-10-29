#nullable enable
namespace SRT.Complaint.Models;

public class EdocAttachmentDelivery
{
    public int Id { get; set; }
    public int EdocDocumentLinkId { get; set; }
    public int ComplaintAttachmentId { get; set; }
    public string Status { get; set; } = "Pending";
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? UploadedAt { get; set; }
    public string? LastError { get; set; }

    public EdocDocumentLink EdocDocumentLink { get; set; } = null!;
    public ComplaintAttachment ComplaintAttachment { get; set; } = null!;
}
