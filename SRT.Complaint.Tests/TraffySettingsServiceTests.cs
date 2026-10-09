using Microsoft.Extensions.Configuration;
using SRT.Complaint.Services;
using Xunit;

namespace SRT.Complaint.Tests;

public class TraffySettingsServiceTests
{
    private static MaskingService CreateMasking()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:Key"] = "lVcPxLPPSBs1SM7hSWJRLokH7BHIkZg9Q3HNegeDf+k="
            })
            .Build();
        return new MaskingService(config);
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsCredentialsThroughEncryption()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        var service = new TraffySettingsService(db, CreateMasking());

        await service.SaveAsync(
            new TraffySettingsInput("https://example.invalid/api", "traffy_user", "s3cret", "151", "whsecret"),
            updatedById: 1);

        var settings = await service.GetAsync();

        Assert.Equal("traffy_user", settings.Username);
        Assert.Equal("s3cret", settings.Password);
        Assert.Equal("151", settings.OrgId);
        Assert.Equal("whsecret", settings.WebhookSecret);
        Assert.True(settings.IsConfigured);
    }

    [Fact]
    public async Task SaveAsync_WithBlankPassword_KeepsExistingEncryptedValue()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        var service = new TraffySettingsService(db, CreateMasking());

        await service.SaveAsync(
            new TraffySettingsInput("https://example.invalid/api", "traffy_user", "original-password", "151", "original-secret"),
            updatedById: 1);

        // Re-save with blank Password/WebhookSecret — should NOT overwrite (standard masked-field UX)
        await service.SaveAsync(
            new TraffySettingsInput("https://example.invalid/api", "traffy_user_renamed", "", "151", ""),
            updatedById: 1);

        var settings = await service.GetAsync();

        Assert.Equal("traffy_user_renamed", settings.Username); // non-secret fields do update
        Assert.Equal("original-password", settings.Password);   // secret fields are preserved
        Assert.Equal("original-secret", settings.WebhookSecret);
    }

    [Fact]
    public async Task GetStatusAsync_NeverExposesDecryptedSecrets()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        var service = new TraffySettingsService(db, CreateMasking());

        await service.SaveAsync(
            new TraffySettingsInput("https://example.invalid/api", "traffy_user", "s3cret", "151", "whsecret"),
            updatedById: 1);

        var status = await service.GetStatusAsync();

        Assert.Equal("traffy_user", status.Username);
        Assert.True(status.HasPassword);
        Assert.True(status.HasWebhookSecret);
    }

    [Fact]
    public async Task GetAsync_ReturnsNotConfigured_WhenNoSettingsSavedYet()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        var service = new TraffySettingsService(db, CreateMasking());

        var settings = await service.GetAsync();

        Assert.False(settings.IsConfigured);
        Assert.Equal("https://publicapi.traffy.in.th/exchange-api", settings.ApiUrl);
    }
}
