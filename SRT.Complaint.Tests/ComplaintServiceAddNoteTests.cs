using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SRT.Complaint.Models;
using SRT.Complaint.Services;
using Xunit;

namespace SRT.Complaint.Tests;

public class ComplaintServiceAddNoteTests
{
    private static IFormFile MakeImageFile(string fileName = "photo.jpg")
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = "image/jpeg" };
    }

    private static ComplaintService CreateService(Data.AppDbContext db, string storagePath, out Mock<IAuditService> auditService)
    {
        auditService = new Mock<IAuditService>();
        var slaService = new Mock<ISlaService>();
        var notificationService = new Mock<INotificationService>();
        var webhookService = new Mock<IWebhookService>();
        var scopeFactory = new Mock<IServiceScopeFactory>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FileUpload:StoragePath"] = storagePath })
            .Build();

        return new ComplaintService(db, slaService.Object, notificationService.Object, auditService.Object, webhookService.Object, scopeFactory.Object, config);
    }

    [Fact]
    public async Task AddNoteAsync_WithAttachments_SavesFilesLinkedToTheNote()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.Complaints.Add(new Models.Complaint
        {
            Id = 1, ReferenceNumber = "SRT-COMPL-2569-0001",
            ReporterName = "x", ReporterPhone = "x", CategoryId = 1
        });
        await db.SaveChangesAsync();

        var tempDir = Path.Combine(Path.GetTempPath(), "srt-tests-" + Guid.NewGuid());
        var service = CreateService(db, tempDir, out var auditService);

        try
        {
            await service.AddNoteAsync(1, authorId: 1, noteType: "PublicReply", content: "แก้ไขเรียบร้อยแล้ว",
                attachments: [MakeImageFile()]);

            var note = Assert.Single(db.ComplaintNotes);
            Assert.Equal("PublicReply", note.NoteType);

            var attachment = Assert.Single(db.ComplaintAttachments);
            Assert.Equal(note.Id, attachment.ComplaintNoteId);
            Assert.Equal(1, attachment.ComplaintId);
            Assert.True(File.Exists(Path.Combine(tempDir, attachment.StoredPath)));

            auditService.Verify(a => a.LogAsync("AddNote", 1, null, "Complaint", "1", It.IsAny<object>(), null, default), Times.Once);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task AddNoteAsync_WithoutAttachments_CreatesNoteOnly()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.Complaints.Add(new Models.Complaint
        {
            Id = 1, ReferenceNumber = "SRT-COMPL-2569-0002",
            ReporterName = "x", ReporterPhone = "x", CategoryId = 1
        });
        await db.SaveChangesAsync();

        var tempDir = Path.Combine(Path.GetTempPath(), "srt-tests-" + Guid.NewGuid());
        var service = CreateService(db, tempDir, out _);

        await service.AddNoteAsync(1, authorId: 1, noteType: "InternalNote", content: "บันทึกภายใน");

        Assert.Single(db.ComplaintNotes);
        Assert.Empty(db.ComplaintAttachments);
    }
}
