#nullable enable
namespace SRT.Complaint.Models;

/// <summary>Single-row table holding Traffy Fondue Exchange API credentials, editable from Admin/ExternalSync.</summary>
public class TraffySettings
{
    public int Id { get; set; }
    public string ApiUrl { get; set; } = "https://publicapi.traffy.in.th/exchange-api";
    public string? Username { get; set; }
    public byte[]? PasswordEncrypted { get; set; }
    public string? OrgId { get; set; }
    public byte[]? WebhookSecretEncrypted { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedById { get; set; }

    public StaffUser? UpdatedBy { get; set; }
}
