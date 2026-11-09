#nullable enable
namespace SRT.Complaint.Models;

public class NotificationDeliveryLog
{
    public long Id { get; set; }
    public string EventKey { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string RecipientMasked { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
}
