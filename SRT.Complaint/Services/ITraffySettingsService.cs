namespace SRT.Complaint.Services;

/// <summary>Manages Traffy Fondue Exchange API credentials, stored encrypted in the DB and
/// editable from Admin/ExternalSync instead of appsettings.json/user-secrets.</summary>
public interface ITraffySettingsService
{
    /// <summary>Decrypted connection settings for the adapter's own use — never expose this over HTTP.</summary>
    Task<TraffyConnectionSettings> GetAsync(CancellationToken ct = default);

    /// <summary>Masked status for the admin settings form — indicates whether secrets are set, never their value.</summary>
    Task<TraffySettingsStatusVm> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Saves settings. A blank Password/WebhookSecret means "keep the existing value" (standard masked-secret-field UX).</summary>
    Task SaveAsync(TraffySettingsInput input, int updatedById, CancellationToken ct = default);
}

public record TraffyConnectionSettings(string ApiUrl, string Username, string Password, string OrgId, string? WebhookSecret)
{
    public bool IsConfigured => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(OrgId);
}

public record TraffySettingsStatusVm(
    string ApiUrl,
    string? Username,
    bool HasPassword,
    string? OrgId,
    bool HasWebhookSecret,
    DateTime? UpdatedAt,
    string? UpdatedByName);

public record TraffySettingsInput(string ApiUrl, string Username, string? Password, string OrgId, string? WebhookSecret);
