#nullable enable
namespace SRT.Complaint.Models;

public class ApiIdempotencyRecord
{
    public long Id { get; set; }
    public int ApiKeyId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string State { get; set; } = "Pending";
    public int? ResponseStatus { get; set; }
    public string? ResponseBody { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);

    public ApiKey ApiKey { get; set; } = null!;
}
