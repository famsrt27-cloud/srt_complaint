#nullable enable
namespace SRT.Complaint.Models;

public class EdocDocumentLink
{
    public int Id { get; set; }
    public int ComplaintId { get; set; }
    public int CreatedById { get; set; }
    public string WID { get; set; } = string.Empty;
    public string BasketId { get; set; } = string.Empty;
    public string? BasketName { get; set; }
    public string? RegisterNumber { get; set; }
    public string? RemoteDocumentDate { get; set; }
    public string SyncStatus { get; set; } = EdocSyncStatuses.Pending;
    public string? RemoteStatusCode { get; set; }
    public string? RemoteStatusDescription { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? LastTrackedAt { get; set; }
    public DateTime? NextTrackingAt { get; set; }
    public DateTime? LeaseExpiresAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Complaint Complaint { get; set; } = null!;
    public StaffUser CreatedBy { get; set; } = null!;
    public ICollection<EdocAttachmentDelivery> AttachmentDeliveries { get; set; } = [];
}

public static class EdocSyncStatuses
{
    public const string Pending = "Pending";
    public const string Sending = "Sending";
    public const string Sent = "Sent";
    public const string SentWithAttachmentErrors = "SentWithAttachmentErrors";
    public const string NeedsReview = "NeedsReview";
    public const string Failed = "Failed";
}
