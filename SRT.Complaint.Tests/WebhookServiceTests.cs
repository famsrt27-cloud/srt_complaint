using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Moq;
using SRT.Complaint.Models;
using SRT.Complaint.Services;

namespace SRT.Complaint.Tests;

public class WebhookServiceTests
{
    [Fact]
    public async Task DispatchPendingOutboxAsync_MalformedEventsJson_DoesNotThrowAndLogsWarning()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.Partners.Add(new Partner { Id = 1, Code = "TEST", Name = "Test" });
        db.ApiKeys.Add(new ApiKey
        {
            Id = 1,
            PartnerId = 1,
            Name = "test",
            KeyHash = "hash",
            KeyPrefix = "prefix",
            KeyType = "External",
            Environment = "Sandbox",
            DataAccessMode = "Own",
            CreatedById = 1,
            IsActive = true
        });
        db.Webhooks.Add(new Webhook
        {
            ApiKeyId = 1,
            Name = "broken",
            TargetUrl = "https://example.invalid/hook",
            SecretHash = "irrelevant",
            Events = "{not-valid-json",
            IsActive = true
        });
        await db.SaveChangesAsync();

        var logger = new Mock<ILogger<WebhookService>>();
        var service = CreateService(db, logger);
        await service.QueueAsync("complaint.created", new { referenceNumber = "TEST-1" }, partnerId: 1);

        var ex = await Record.ExceptionAsync(() => service.DispatchPendingOutboxAsync());

        Assert.Null(ex);
        logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => Convert.ToString(state)!.Contains("malformed")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchPendingOutboxAsync_NoActiveWebhooks_MarksMessageProcessed()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        var service = CreateService(db, new Mock<ILogger<WebhookService>>());
        await service.QueueAsync("complaint.created", new { referenceNumber = "TEST-1" }, partnerId: null);

        await service.DispatchPendingOutboxAsync();

        var message = Assert.Single(db.WebhookOutboxMessages);
        Assert.NotNull(message.ProcessedAt);
        Assert.Empty(db.WebhookDeliveryLogs);
    }

    private static WebhookService CreateService(Data.AppDbContext db, Mock<ILogger<WebhookService>> logger)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var masking = new Mock<IMaskingService>();
        var urlValidator = new Mock<IWebhookUrlValidator>();
        urlValidator.Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, null));
        return new WebhookService(
            db,
            httpClientFactory.Object,
            masking.Object,
            new EphemeralDataProtectionProvider(),
            urlValidator.Object,
            logger.Object);
    }
}
