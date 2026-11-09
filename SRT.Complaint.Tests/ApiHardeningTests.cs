using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Moq;
using SRT.Complaint.Models;
using SRT.Complaint.Services;

namespace SRT.Complaint.Tests;

public class ApiHardeningTests
{
    [Theory]
    [InlineData(ComplaintWorkflowSteps.Received, "Closed")]
    [InlineData(ComplaintWorkflowSteps.Screening, "Resolved")]
    [InlineData(ComplaintWorkflowSteps.Investigation, "Rejected")]
    public void StatusPolicy_RejectsStatusThatWouldSkipWorkflow(string workflowStep, string status)
    {
        Assert.False(ComplaintStatusTransitionPolicy.CanSetStatus(workflowStep, status));
    }

    [Theory]
    [InlineData(ComplaintWorkflowSteps.Summary, "Resolved")]
    [InlineData(ComplaintWorkflowSteps.Notification, "Closed")]
    [InlineData(ComplaintWorkflowSteps.WaitingInfo, "InProgress")]
    public void StatusPolicy_AllowsOnlyStatusRelevantToCurrentWorkflow(string workflowStep, string status)
    {
        Assert.True(ComplaintStatusTransitionPolicy.CanSetStatus(workflowStep, status));
    }

    [Theory]
    [InlineData(ComplaintWorkflowSteps.Summary, "Resolved")]
    [InlineData(ComplaintWorkflowSteps.Notification, "Resolved")]
    [InlineData(ComplaintWorkflowSteps.Completed, "Closed")]
    [InlineData(ComplaintWorkflowSteps.Rejected, "Rejected")]
    public void WorkflowPolicy_DerivesCanonicalStatus(string workflowStep, string expectedStatus)
    {
        Assert.Equal(expectedStatus, ComplaintStatusTransitionPolicy.StatusForWorkflow(workflowStep));
    }

    [Fact]
    public async Task RateLimitStore_ConcurrentRequests_CannotExceedPermitLimit()
    {
        var store = new ApiRateLimitStore();
        var now = new DateTimeOffset(2026, 11, 3, 10, 15, 20, TimeSpan.Zero);

        var results = await Task.WhenAll(Enumerable.Range(0, 100)
            .Select(_ => store.AcquireAsync(42, 10, now)));

        Assert.Equal(10, results.Count(x => x.IsAllowed));
        Assert.Equal(90, results.Count(x => !x.IsAllowed));
        Assert.All(results.Where(x => !x.IsAllowed), x => Assert.Equal(0, x.Remaining));
    }

    [Fact]
    public async Task RateLimitStore_AfterReset_StartsNewWindow()
    {
        var store = new ApiRateLimitStore();
        var now = new DateTimeOffset(2026, 11, 3, 10, 15, 59, TimeSpan.Zero);

        Assert.True((await store.AcquireAsync(7, 1, now)).IsAllowed);
        Assert.False((await store.AcquireAsync(7, 1, now)).IsAllowed);
        var afterReset = await store.AcquireAsync(7, 1, now.AddSeconds(1));

        Assert.True(afterReset.IsAllowed);
        Assert.Equal(0, afterReset.Remaining);
    }

    [Fact]
    public async Task DeleteForApiKeyAsync_DoesNotDeleteAnotherKeysWebhook()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.Partners.AddRange(
            new Partner { Id = 1, Code = "A", Name = "A", IsActive = true },
            new Partner { Id = 2, Code = "B", Name = "B", IsActive = true });
        db.ApiKeys.AddRange(ApiKey(10, 1), ApiKey(20, 2));
        db.Webhooks.Add(new Webhook
        {
            Id = 99,
            ApiKeyId = 20,
            Name = "partner-b",
            TargetUrl = "https://example.com/hook",
            SecretHash = "secret",
            Events = "[]",
            IsActive = true
        });
        await db.SaveChangesAsync();
        var service = CreateWebhookService(db);

        var deleted = await service.DeleteForApiKeyAsync(99, 10);

        Assert.False(deleted);
        Assert.True((await db.Webhooks.FindAsync(99))!.IsActive);
    }

    [Fact]
    public async Task CreateWebhookAsync_RejectsMoreThanTenActiveWebhooksPerKey()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.Partners.Add(new Partner { Id = 1, Code = "A", Name = "A", IsActive = true });
        db.ApiKeys.Add(ApiKey(10, 1));
        for (var i = 1; i <= WebhookService.MaxActiveWebhooksPerApiKey; i++)
        {
            db.Webhooks.Add(new Webhook
            {
                ApiKeyId = 10,
                Name = $"hook-{i}",
                TargetUrl = $"https://example.com/hook/{i}",
                SecretHash = "secret",
                Events = "[]",
                IsActive = true
            });
        }
        await db.SaveChangesAsync();
        var service = CreateWebhookService(db);

        var action = () => service.CreateAsync(
            10, "one-too-many", "https://example.com/new", ["complaint.created"]);

        await Assert.ThrowsAsync<InvalidOperationException>(action);
    }

    private static ApiKey ApiKey(int id, int partnerId) => new()
    {
        Id = id,
        PartnerId = partnerId,
        Name = $"key-{id}",
        KeyHash = $"hash-{id}",
        KeyPrefix = $"prefix-{id}",
        KeyType = "External",
        Environment = "Sandbox",
        DataAccessMode = "Own",
        CreatedById = 1,
        IsActive = true
    };

    private static WebhookService CreateWebhookService(Data.AppDbContext db)
    {
        var validator = new Mock<IWebhookUrlValidator>();
        validator.Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, null));
        return new WebhookService(
            db,
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<IMaskingService>(),
            new EphemeralDataProtectionProvider(),
            validator.Object,
            Mock.Of<ILogger<WebhookService>>());
    }
}
