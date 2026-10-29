#nullable enable
namespace SRT.Complaint.Models;

public class WebhookOutboxMessage
{
    public long Id { get; set; }
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = string.Empty;
    public int? PartnerId { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? LastError { get; set; }

    public Partner? Partner { get; set; }
    public ICollection<WebhookDeliveryLog> DeliveryLogs { get; set; } = new List<WebhookDeliveryLog>();
}
