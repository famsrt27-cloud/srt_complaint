using Microsoft.Extensions.Configuration;
using Moq;
using SRT.Complaint.Models;
using SRT.Complaint.Services;

namespace SRT.Complaint.Tests;

public class ApiKeyServiceTests
{
    [Fact]
    public async Task CreateAndValidateAsync_ReturnsPartnerScopedKey()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.Partners.Add(new Partner { Id = 10, Code = "EDOC", Name = "eDOC", IsActive = true });
        await db.SaveChangesAsync();
        var service = CreateService(db, "Sandbox");

        var (key, rawKey) = await service.CreateAsync(new CreateApiKeyRequest(
            10, "EDOC sandbox", "External", "Sandbox", "Own", "test",
            ["metadata:read", "complaints:write"], 60, "203.0.113.10",
            DateTime.UtcNow.AddDays(30), 1));
        var validated = await service.ValidateAsync(rawKey);

        Assert.StartsWith("srt_test_", rawKey);
        Assert.Equal(10, key.PartnerId);
        Assert.NotNull(validated);
        Assert.Equal("EDOC", validated!.Partner.Code);
        Assert.Equal("Own", validated.DataAccessMode);
    }

    [Fact]
    public async Task ExternalKeyCannotReceiveAllDataAccess()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.Partners.Add(new Partner { Id = 11, Code = "CRM", Name = "CRM", IsActive = true });
        await db.SaveChangesAsync();
        var service = CreateService(db, "Production");

        var action = () => service.CreateAsync(new CreateApiKeyRequest(
            11, "CRM", "External", "Production", "All", null,
            ["complaints:read"], 60, null, DateTime.UtcNow.AddDays(30), 1));

        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    [Fact]
    public async Task SandboxKey_IsRejectedByProductionInstance()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.Partners.Add(new Partner { Id = 12, Code = "MOBILE", Name = "Mobile", IsActive = true });
        db.ApiKeys.Add(new ApiKey
        {
            Id = 21,
            PartnerId = 12,
            Name = "Sandbox key",
            KeyType = "External",
            Environment = "Sandbox",
            DataAccessMode = "Own",
            KeyPrefix = "srt_test_example",
            KeyHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("srt_test_example-secret"))).ToLowerInvariant(),
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedById = 1
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, "Production");
        var validated = await service.ValidateAsync("srt_test_example-secret");

        Assert.Null(validated);
    }

    private static ApiKeyService CreateService(Data.AppDbContext db, string environment)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiManagement:Environment"] = environment
            })
            .Build();
        return new ApiKeyService(db, new Mock<IAuditService>().Object, configuration);
    }
}
