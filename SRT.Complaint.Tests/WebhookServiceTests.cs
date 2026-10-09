using Microsoft.Extensions.Logging;
using Moq;
using SRT.Complaint.Models;
using SRT.Complaint.Services;
using Xunit;

namespace SRT.Complaint.Tests;

public class WebhookServiceTests
{
    [Fact]
    public async Task TriggerAsync_MalformedEventsJson_DoesNotThrowAndLogsWarning()
    {
        await using var db = TestHelpers.CreateAppDbContext();
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

        var httpClientFactory = new Mock<IHttpClientFactory>();
        var masking = new Mock<IMaskingService>();
        var logger = new Mock<ILogger<WebhookService>>();
        var service = new WebhookService(db, httpClientFactory.Object, masking.Object, logger.Object);

        var ex = await Record.ExceptionAsync(() => service.TriggerAsync("complaint.created", new { }));

        Assert.Null(ex);
        logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("malformed")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task TriggerAsync_NoActiveWebhooks_ReturnsWithoutError()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var masking = new Mock<IMaskingService>();
        var logger = new Mock<ILogger<WebhookService>>();
        var service = new WebhookService(db, httpClientFactory.Object, masking.Object, logger.Object);

        var ex = await Record.ExceptionAsync(() => service.TriggerAsync("complaint.created", new { }));

        Assert.Null(ex);
    }
}
