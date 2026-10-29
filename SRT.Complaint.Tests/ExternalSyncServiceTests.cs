using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SRT.Complaint.Models;
using SRT.Complaint.Services;
using Xunit;

namespace SRT.Complaint.Tests;

public class ExternalSyncServiceTests
{
    private const string SystemKey = "test_external";

    private static ExternalSyncService CreateService(
        Data.AppDbContext db, out Mock<IComplaintService> complaintService)
    {
        complaintService = new Mock<IComplaintService>();
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var config = new ConfigurationBuilder().Build();
        var logger = new Mock<ILogger<ExternalSyncService>>();
        return new ExternalSyncService([], db, complaintService.Object, httpClientFactory.Object, config, logger.Object);
    }

    private static ExternalComplaintDto SampleDto(string externalId = "TICKET-1") => new(
        ExternalId: externalId,
        Description: "รายละเอียด",
        Address: "สถานีกรุงเทพ",
        CategoryHint: null,
        IncidentDate: null,
        ReporterName: "ผู้แจ้ง",
        ReporterPhone: "0812345678",
        RawStatus: "รอรับเรื่อง");

    [Fact]
    public async Task ImportFromWebhookAsync_SkipsDuplicate_WhenAlreadyImported()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.Complaints.Add(new Models.Complaint
        {
            ReferenceNumber = "SRT-COMPL-2569-0001",
            ExternalSystem = SystemKey,
            ExternalId = "TICKET-1",
            ReporterName = "x", ReporterPhone = "x", CategoryId = 1
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, out var complaintService);

        var (imported, refNumber) = await service.ImportFromWebhookAsync(SampleDto(), SystemKey);

        Assert.False(imported);
        Assert.Null(refNumber);
        complaintService.Verify(c => c.SubmitAsync(It.IsAny<SubmitComplaintRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportFromWebhookAsync_Imports_AndStampsExternalFields_WhenNotDuplicate()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ComplaintCategories.Add(new ComplaintCategory { Name = "อื่นๆ", IsActive = true, SortOrder = 1 });
        await db.SaveChangesAsync();

        var service = CreateService(db, out var complaintService);
        complaintService
            .Setup(c => c.SubmitAsync(It.IsAny<SubmitComplaintRequest>(), It.IsAny<CancellationToken>()))
            .Returns((SubmitComplaintRequest _, CancellationToken _) =>
            {
                var complaint = new Models.Complaint
                {
                    ReferenceNumber = "SRT-COMPL-2569-0002",
                    ReporterName = "x", ReporterPhone = "x", CategoryId = 1
                };
                db.Complaints.Add(complaint);
                return Task.FromResult(complaint);
            });

        var (imported, refNumber) = await service.ImportFromWebhookAsync(SampleDto("TICKET-2"), SystemKey);

        Assert.True(imported);
        Assert.Equal("SRT-COMPL-2569-0002", refNumber);
        var saved = db.Complaints.Single(c => c.ExternalId == "TICKET-2");
        Assert.Equal(SystemKey, saved.ExternalSystem);
        Assert.Equal(SystemKey, saved.Channel);
    }

    [Fact]
    public async Task ImportFromWebhookAsync_Throws_WhenNoActiveCategory()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        var service = CreateService(db, out _);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ImportFromWebhookAsync(SampleDto("TICKET-3"), SystemKey));
    }

    [Fact]
    public async Task ApplyStatusUpdateAsync_ReturnsFalse_WhenComplaintNotFound()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        var service = CreateService(db, out _);

        var found = await service.ApplyStatusUpdateAsync(SystemKey, "UNKNOWN", "Resolved", "done");

        Assert.False(found);
    }

    [Fact]
    public async Task ApplyStatusUpdateAsync_UpdatesStatusAndAddsNote_WhenStatusChanges()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.Complaints.Add(new Models.Complaint
        {
            ReferenceNumber = "SRT-COMPL-2569-0003",
            ExternalSystem = SystemKey,
            ExternalId = "TICKET-4",
            Status = "Pending",
            ReporterName = "x", ReporterPhone = "x", CategoryId = 1
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, out _);

        var found = await service.ApplyStatusUpdateAsync(SystemKey, "TICKET-4", "Resolved", "แก้ไขแล้ว");

        Assert.True(found);
        var complaint = db.Complaints.Single(c => c.ExternalId == "TICKET-4");
        Assert.Equal("Resolved", complaint.Status);
        Assert.Null(complaint.ClosedAt);
        Assert.Single(db.ComplaintNotes);
    }

    [Fact]
    public async Task ApplyStatusUpdateAsync_ReturnsTrueWithoutWritingNote_WhenStatusUnmappedOrUnchanged()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.Complaints.Add(new Models.Complaint
        {
            ReferenceNumber = "SRT-COMPL-2569-0005",
            ExternalSystem = SystemKey,
            ExternalId = "TICKET-5",
            Status = "Pending",
            ReporterName = "x", ReporterPhone = "x", CategoryId = 1
        });
        await db.SaveChangesAsync();
        var service = CreateService(db, out _);

        var found = await service.ApplyStatusUpdateAsync(SystemKey, "TICKET-5", newStatus: null, note: null);

        Assert.True(found);
        Assert.Empty(db.ComplaintNotes);
        Assert.Equal("Pending", db.Complaints.Single().Status);
    }

    [Fact]
    public async Task ImportFromWebhookAsync_MatchesCategoryByName_WhenCategoryHintMatchesExactly()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ComplaintCategories.Add(new ComplaintCategory { Id = 1, Name = "อื่นๆ", IsActive = true, SortOrder = 1 });
        db.ComplaintCategories.Add(new ComplaintCategory { Id = 2, Name = "ถนน", IsActive = true, SortOrder = 2 });
        await db.SaveChangesAsync();

        var service = CreateService(db, out var complaintService);
        int? capturedCategoryId = null;
        complaintService
            .Setup(c => c.SubmitAsync(It.IsAny<SubmitComplaintRequest>(), It.IsAny<CancellationToken>()))
            .Returns((SubmitComplaintRequest req, CancellationToken _) =>
            {
                capturedCategoryId = req.CategoryId;
                var complaint = new Models.Complaint
                {
                    ReferenceNumber = "SRT-COMPL-2569-0006",
                    ReporterName = "x", ReporterPhone = "x", CategoryId = req.CategoryId
                };
                db.Complaints.Add(complaint);
                return Task.FromResult(complaint);
            });

        var dto = SampleDto("TICKET-6") with { CategoryHint = "ถนน" };
        await service.ImportFromWebhookAsync(dto, SystemKey);

        Assert.Equal(2, capturedCategoryId);
    }

    [Fact]
    public async Task ImportFromWebhookAsync_FallsBackToDefaultCategory_WhenCategoryHintDoesNotMatch()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ComplaintCategories.Add(new ComplaintCategory { Id = 1, Name = "อื่นๆ", IsActive = true, SortOrder = 1 });
        await db.SaveChangesAsync();

        var service = CreateService(db, out var complaintService);
        int? capturedCategoryId = null;
        complaintService
            .Setup(c => c.SubmitAsync(It.IsAny<SubmitComplaintRequest>(), It.IsAny<CancellationToken>()))
            .Returns((SubmitComplaintRequest req, CancellationToken _) =>
            {
                capturedCategoryId = req.CategoryId;
                var complaint = new Models.Complaint
                {
                    ReferenceNumber = "SRT-COMPL-2569-0007",
                    ReporterName = "x", ReporterPhone = "x", CategoryId = req.CategoryId
                };
                db.Complaints.Add(complaint);
                return Task.FromResult(complaint);
            });

        var dto = SampleDto("TICKET-7") with { CategoryHint = "ประเภทที่ไม่มีในระบบ" };
        await service.ImportFromWebhookAsync(dto, SystemKey);

        Assert.Equal(1, capturedCategoryId);
    }

    private sealed class FakeAdapter(string systemKey, ExternalFetchResult result) : IExternalSystemAdapter
    {
        public string SystemKey => systemKey;
        public string DisplayName => systemKey;
        public string Description => "";
        public bool IsConfigured => true;
        public Task<ExternalFetchResult> FetchNewAsync(CancellationToken ct = default) => Task.FromResult(result);
        public Task PushStatusAsync(string externalId, string newStatus, string? note, CancellationToken ct = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task SyncAsync_AcceptsNullTriggeredBy_ForScheduledBackgroundSync()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ComplaintCategories.Add(new ComplaintCategory { Name = "อื่นๆ", IsActive = true, SortOrder = 1 });
        await db.SaveChangesAsync();

        var complaintService = new Mock<IComplaintService>();
        complaintService
            .Setup(c => c.SubmitAsync(It.IsAny<SubmitComplaintRequest>(), It.IsAny<CancellationToken>()))
            .Returns((SubmitComplaintRequest _, CancellationToken _) =>
            {
                var complaint = new Models.Complaint
                {
                    ReferenceNumber = "SRT-COMPL-2569-0011",
                    ReporterName = "x", ReporterPhone = "x", CategoryId = 1
                };
                db.Complaints.Add(complaint);
                return Task.FromResult(complaint);
            });

        var dto = SampleDto("TICKET-10");
        var adapter = new FakeAdapter(SystemKey, new ExternalFetchResult([dto]));
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var config = new ConfigurationBuilder().Build();
        var logger = new Mock<ILogger<ExternalSyncService>>();
        var service = new ExternalSyncService([adapter], db, complaintService.Object, httpClientFactory.Object, config, logger.Object);

        var log = await service.SyncAsync(SystemKey, triggeredById: null);

        Assert.Equal("Success", log.SyncStatus);
        Assert.Null(log.TriggeredById);
        Assert.Equal(1, log.NewCount);
    }

    private sealed class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response);
    }

    [Fact]
    public async Task ImportFromWebhookAsync_DownloadsAndSavesPhoto_WhenContentTypeIsImage()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ComplaintCategories.Add(new ComplaintCategory { Name = "อื่นๆ", IsActive = true, SortOrder = 1 });
        await db.SaveChangesAsync();

        var complaintService = new Mock<IComplaintService>();
        complaintService
            .Setup(c => c.SubmitAsync(It.IsAny<SubmitComplaintRequest>(), It.IsAny<CancellationToken>()))
            .Returns((SubmitComplaintRequest _, CancellationToken _) =>
            {
                var complaint = new Models.Complaint
                {
                    ReferenceNumber = "SRT-COMPL-2569-0008",
                    ReporterName = "x", ReporterPhone = "x", CategoryId = 1
                };
                db.Complaints.Add(complaint);
                return Task.FromResult(complaint);
            });

        var responseMessage = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0])
        };
        responseMessage.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

        var httpClient = new HttpClient(new FakeHttpMessageHandler(responseMessage));
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var tempDir = Path.Combine(Path.GetTempPath(), "srt-tests-" + Guid.NewGuid());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileUpload:StoragePath"] = tempDir })
            .Build();
        var logger = new Mock<ILogger<ExternalSyncService>>();

        var service = new ExternalSyncService([], db, complaintService.Object, httpClientFactory.Object, config, logger.Object);
        var dto = SampleDto("TICKET-8") with { PhotoUrls = ["https://example.invalid/photo.jpg"] };

        try
        {
            var (imported, _) = await service.ImportFromWebhookAsync(dto, SystemKey);

            Assert.True(imported);
            var attachment = Assert.Single(db.ComplaintAttachments);
            Assert.Equal("image/jpeg", attachment.MimeType);
            Assert.True(File.Exists(Path.Combine(tempDir, attachment.StoredPath)));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ImportFromWebhookAsync_SkipsPhoto_WhenContentTypeIsNotImage()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ComplaintCategories.Add(new ComplaintCategory { Name = "อื่นๆ", IsActive = true, SortOrder = 1 });
        await db.SaveChangesAsync();

        var complaintService = new Mock<IComplaintService>();
        complaintService
            .Setup(c => c.SubmitAsync(It.IsAny<SubmitComplaintRequest>(), It.IsAny<CancellationToken>()))
            .Returns((SubmitComplaintRequest _, CancellationToken _) =>
            {
                var complaint = new Models.Complaint
                {
                    ReferenceNumber = "SRT-COMPL-2569-0009",
                    ReporterName = "x", ReporterPhone = "x", CategoryId = 1
                };
                db.Complaints.Add(complaint);
                return Task.FromResult(complaint);
            });

        var responseMessage = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([0x4D, 0x5A]) // not an image
        };
        responseMessage.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

        var httpClient = new HttpClient(new FakeHttpMessageHandler(responseMessage));
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var tempDir = Path.Combine(Path.GetTempPath(), "srt-tests-" + Guid.NewGuid());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileUpload:StoragePath"] = tempDir })
            .Build();
        var logger = new Mock<ILogger<ExternalSyncService>>();

        var service = new ExternalSyncService([], db, complaintService.Object, httpClientFactory.Object, config, logger.Object);
        var dto = SampleDto("TICKET-9") with { PhotoUrls = ["https://example.invalid/malicious.exe"] };

        try
        {
            var (imported, _) = await service.ImportFromWebhookAsync(dto, SystemKey);

            Assert.True(imported);
            Assert.Empty(db.ComplaintAttachments);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }
}
