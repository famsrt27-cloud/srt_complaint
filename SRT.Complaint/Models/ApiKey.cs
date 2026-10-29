#nullable enable
namespace SRT.Complaint.Models;

public class ApiKey
{
    public int Id { get; set; }
    public int PartnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyType { get; set; } = string.Empty;
    public string Environment { get; set; } = "Production";
    public string DataAccessMode { get; set; } = "Own";
    public string? Purpose { get; set; }
    public string KeyPrefix { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public int RateLimitPerMin { get; set; } = 60;
    public string? AllowedIps { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public int CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public int? RevokedById { get; set; }
    public string? RevokedReason { get; set; }
    public int? RotatedToKeyId { get; set; }
    public DateTime? RotationGraceEndsAt { get; set; }

    public Partner Partner { get; set; } = null!;
    public StaffUser CreatedBy { get; set; } = null!;
    public StaffUser? RevokedBy { get; set; }
    public ICollection<ApiKeyScope> Scopes { get; set; } = new List<ApiKeyScope>();
    public ICollection<ApiRequestLog> RequestLogs { get; set; } = new List<ApiRequestLog>();
    public ICollection<ApiIdempotencyRecord> IdempotencyRecords { get; set; } = new List<ApiIdempotencyRecord>();
    public ICollection<Webhook> Webhooks { get; set; } = new List<Webhook>();
}
