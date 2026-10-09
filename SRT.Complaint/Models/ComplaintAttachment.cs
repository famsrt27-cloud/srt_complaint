#nullable enable
namespace SRT.Complaint.Models;

public class ComplaintAttachment
{
    public int Id { get; set; }
    public int ComplaintId { get; set; }
    public int? ComplaintNoteId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public string? MimeType { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public Complaint Complaint { get; set; } = null!;
    public ComplaintNote? Note { get; set; }
}
