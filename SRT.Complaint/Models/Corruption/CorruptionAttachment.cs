#nullable enable
namespace SRT.Complaint.Models.Corruption;

public class CorruptionAttachment
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public CorruptionReport Report { get; set; } = null!;
}
