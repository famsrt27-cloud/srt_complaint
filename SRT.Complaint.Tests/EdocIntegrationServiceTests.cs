using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SRT.Complaint.Models;
using SRT.Complaint.Services;

namespace SRT.Complaint.Tests;

public class EdocIntegrationServiceTests
{
    [Fact]
    public async Task QueueAndSendNow_CreatesDocumentInSelectedBasket()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        var actor = new StaffUser
        {
            EmployeeCode = "1234567", FullName = "เจ้าหน้าที่", PasswordHash = "x",
            Role = "GeneralOfficer", IsActive = true
        };
        var category = new ComplaintCategory
        {
            Name = "ทดสอบ", DepartmentName = "ฝ่ายปลายทาง", IsActive = true
        };
        var complaint = new Models.Complaint
        {
            ReferenceNumber = "SRT-2569-0001", ReporterName = "ผู้แจ้ง", ReporterPhone = "0800000000",
            Description = "รายละเอียดเรื่องร้องเรียน", Category = category, AssignedTo = actor,
            CreatedAt = new DateTime(2026, 10, 27, 4, 0, 0, DateTimeKind.Utc)
        };
        db.AddRange(actor, category, complaint);
        await db.SaveChangesAsync();

        EdocCreateCommand? received = null;
        var client = new Mock<IEdocApiClient>();
        client.SetupGet(x => x.IsConfigured).Returns(true);
        client.Setup(x => x.CreateDocumentAsync(It.IsAny<EdocCreateCommand>(), It.IsAny<CancellationToken>()))
            .Callback<EdocCreateCommand, CancellationToken>((command, _) => received = command)
            .ReturnsAsync((EdocCreateCommand command, CancellationToken _) =>
                new EdocCreateResult(command.WID, "REG-1", "2569/10/27"));
        var audit = new Mock<IAuditService>();
        audit.Setup(x => x.LogAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var options = new EdocOptions
        {
            Enabled = true,
            Baskets = [new EdocBasketOption { Id = "1234567890123", Name = "ฝ่ายทดสอบ" }],
            CategoryBasketMappings = new Dictionary<string, string>
            {
                [category.Id.ToString()] = "1234567890123"
            },
            SarabanUsername = "saraban-user",
            FromName = "งานรับเรื่องร้องเรียน",
            DefaultSendTo = "ฝ่ายปลายทาง",
            TrackingIntervalMinutes = 30
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var service = new EdocIntegrationService(db, client.Object, Options.Create(options), configuration,
            audit.Object, Mock.Of<ILogger<EdocIntegrationService>>());

        var queued = await service.QueueAsync(complaint.Id, actor.Id, "1234567890123");
        var sent = await service.SendNowAsync(complaint.Id);

        Assert.Equal("1234567890123", queued.BasketId);
        Assert.NotNull(received);
        Assert.Equal("1234567890123", received!.BasketId);
        Assert.Equal(EdocSyncStatuses.Sent, sent.SyncStatus);
        Assert.Equal("REG-1", sent.RegisterNumber);
        client.Verify(x => x.CreateDocumentAsync(It.IsAny<EdocCreateCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueueAsync_RejectsBasketOutsideConfiguredAllowlist()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        var actor = new StaffUser { EmployeeCode = "1234567", FullName = "เจ้าหน้าที่", PasswordHash = "x", Role = "GeneralOfficer" };
        var category = new ComplaintCategory { Name = "ทดสอบ", DepartmentName = "ฝ่ายปลายทาง" };
        var complaint = new Models.Complaint
        {
            ReferenceNumber = "SRT-2569-0002", ReporterName = "ผู้แจ้ง", ReporterPhone = "0800000000",
            Description = "รายละเอียด", Category = category
        };
        db.AddRange(actor, category, complaint);
        await db.SaveChangesAsync();
        var client = new Mock<IEdocApiClient>();
        client.SetupGet(x => x.IsConfigured).Returns(true);
        var options = new EdocOptions
        {
            Enabled = true,
            Baskets = [new EdocBasketOption { Id = "1234567890123", Name = "ฝ่ายทดสอบ" }],
            DefaultSendTo = "ฝ่ายปลายทาง"
        };
        var service = new EdocIntegrationService(db, client.Object, Options.Create(options),
            new ConfigurationBuilder().Build(), Mock.Of<IAuditService>(), Mock.Of<ILogger<EdocIntegrationService>>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.QueueAsync(complaint.Id, actor.Id, "9999999999999"));
    }
}
