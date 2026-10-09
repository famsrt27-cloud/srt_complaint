using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;

namespace SRT.Complaint.Services;

public class TraffySettingsService(AppDbContext db, IMaskingService masking) : ITraffySettingsService
{
    public async Task<TraffyConnectionSettings> GetAsync(CancellationToken ct = default)
    {
        var settings = await GetOrCreateAsync(ct);
        return new TraffyConnectionSettings(
            ApiUrl:        settings.ApiUrl,
            Username:      settings.Username ?? "",
            Password:      settings.PasswordEncrypted != null ? masking.Decrypt(settings.PasswordEncrypted) : "",
            OrgId:         settings.OrgId ?? "",
            WebhookSecret: settings.WebhookSecretEncrypted != null ? masking.Decrypt(settings.WebhookSecretEncrypted) : null);
    }

    public async Task<TraffySettingsStatusVm> GetStatusAsync(CancellationToken ct = default)
    {
        var settings = await db.TraffySettings
            .Include(s => s.UpdatedBy)
            .FirstOrDefaultAsync(ct);

        return new TraffySettingsStatusVm(
            ApiUrl:          settings?.ApiUrl ?? "https://publicapi.traffy.in.th/exchange-api",
            Username:        settings?.Username,
            HasPassword:     settings?.PasswordEncrypted != null,
            OrgId:           settings?.OrgId,
            HasWebhookSecret: settings?.WebhookSecretEncrypted != null,
            UpdatedAt:       settings?.UpdatedAt,
            UpdatedByName:   settings?.UpdatedBy?.FullName);
    }

    public async Task SaveAsync(TraffySettingsInput input, int updatedById, CancellationToken ct = default)
    {
        var settings = await GetOrCreateAsync(ct);

        settings.ApiUrl = string.IsNullOrWhiteSpace(input.ApiUrl)
            ? "https://publicapi.traffy.in.th/exchange-api"
            : input.ApiUrl.Trim();
        settings.Username = input.Username.Trim();
        settings.OrgId    = input.OrgId.Trim();

        if (!string.IsNullOrWhiteSpace(input.Password))
            settings.PasswordEncrypted = masking.Encrypt(input.Password);

        if (!string.IsNullOrWhiteSpace(input.WebhookSecret))
            settings.WebhookSecretEncrypted = masking.Encrypt(input.WebhookSecret);

        settings.UpdatedAt   = DateTime.UtcNow;
        settings.UpdatedById = updatedById;

        await db.SaveChangesAsync(ct);
    }

    private async Task<Models.TraffySettings> GetOrCreateAsync(CancellationToken ct)
    {
        var settings = await db.TraffySettings.FirstOrDefaultAsync(ct);
        if (settings != null) return settings;

        settings = new Models.TraffySettings();
        db.TraffySettings.Add(settings);
        await db.SaveChangesAsync(ct);
        return settings;
    }
}
