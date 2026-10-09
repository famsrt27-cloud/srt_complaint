#nullable enable
namespace SRT.Complaint.Models;

public class ComplaintNote
{
    public int Id { get; set; }
    public int ComplaintId { get; set; }
    public int? AuthorId { get; set; }
    public string NoteType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Complaint Complaint { get; set; } = null!;
    public StaffUser? Author { get; set; }
    public ICollection<ComplaintAttachment> Attachments { get; set; } = new List<ComplaintAttachment>();
}
