using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SRT.Complaint.Models;
using SRT.Complaint.Services;

namespace SRT.Complaint.Tests;

public class ComplaintServiceUpdate2Tests
{
    private static ComplaintService CreateService(Data.AppDbContext db, out Mock<IAuditService> audit)
        => CreateService(db, out audit, out _);

    private static ComplaintService CreateService(Data.AppDbContext db, out Mock<IAuditService> audit,
        out Mock<IWebhookService> webhook)
    {
        audit = new Mock<IAuditService>();
        webhook = new Mock<IWebhookService>();
        return new ComplaintService(
            db,
            Mock.Of<ISlaService>(),
            Mock.Of<INotificationService>(),
            audit.Object,
            webhook.Object,
            Mock.Of<IServiceScopeFactory>(),
            new ConfigurationBuilder().AddInMemoryCollection().Build());
    }

    [Fact]
    public async Task GetQueueAsync_PutsOldestUnfinishedComplaintsBeforeCompletedComplaints()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ComplaintCategories.Add(new ComplaintCategory { Id = 1, Name = "บริการ", IsActive = true });
        db.Complaints.AddRange(
            Complaint(1, "SRT-COMPL-2569-0001", "Closed", new DateTime(2026, 1, 1), "เรื่องเก่าที่ปิดแล้ว"),
            Complaint(2, "SRT-COMPL-2569-0002", "Pending", new DateTime(2026, 1, 2), "พัดลมตกใส่ศีรษะ"),
            Complaint(3, "SRT-COMPL-2569-0003", "InProgress", new DateTime(2026, 1, 3), "กำลังตรวจสอบ"),
            Complaint(4, "SRT-COMPL-2569-0004", "Resolved", new DateTime(2026, 1, 4), "ข้อมูลเดิมที่ยังต้องปิดงาน"));
        await db.SaveChangesAsync();

        var service = CreateService(db, out _);
        var result = await service.GetQueueAsync(new ComplaintQueueFilter(PageSize: 20));

        Assert.Equal([2, 3, 4, 1], result.Select(item => item.Id).ToArray());
    }

    [Theory]
    [InlineData("พัดลม")]
    [InlineData("0812345678")]
    [InlineData("สมชาย")]
    public async Task GetQueueAsync_SearchesDescriptionPhoneAndReporterName(string search)
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ComplaintCategories.Add(new ComplaintCategory { Id = 1, Name = "บริการ", IsActive = true });
        db.Complaints.Add(Complaint(1, "SRT-COMPL-2569-0001", "Pending", new DateTime(2026, 1, 2), "พัดลมตกใส่ศีรษะ"));
        await db.SaveChangesAsync();

        var service = CreateService(db, out _);
        var result = await service.GetQueueAsync(new ComplaintQueueFilter(Search: search));

        Assert.Single(result);
    }

    [Fact]
    public async Task UpdateClassificationAsync_ChangesCategorySubcategoryAndDepartment()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ComplaintCategories.AddRange(
            new ComplaintCategory { Id = 1, Name = "เดิม", DepartmentName = "ฝ่ายเดิม", IsActive = true },
            new ComplaintCategory { Id = 2, Name = "ใหม่", DepartmentName = "ฝ่ายใหม่", IsActive = true });
        db.ComplaintSubCategories.Add(new ComplaintSubCategory { Id = 20, CategoryId = 2, Name = "หัวข้อใหม่", IsActive = true });
        db.Complaints.Add(Complaint(1, "SRT-COMPL-2569-0001", "Pending", new DateTime(2026, 1, 2), "รายละเอียด"));
        await db.SaveChangesAsync();

        var service = CreateService(db, out var audit);
        await service.UpdateClassificationAsync(1, 2, 20, "ฝ่ายใหม่", 99);

        var complaint = await db.Complaints.FindAsync(1);
        Assert.NotNull(complaint);
        Assert.Equal(2, complaint.CategoryId);
        Assert.Equal(20, complaint.SubCategoryId);
        Assert.Equal("ฝ่ายใหม่", complaint.ResponsibleDepartment);
        audit.Verify(item => item.LogAsync(
            "UpdateComplaintClassification", 99, null, "Complaint", "1",
            It.IsAny<object>(), null, default), Times.Once);
    }

    [Fact]
    public async Task CloseAsync_FromInProgress_ClosesCaseAndStoresPublicSummary()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ComplaintCategories.Add(new ComplaintCategory { Id = 1, Name = "บริการ", IsActive = true });
        db.Complaints.Add(Complaint(1, "SRT-COMPL-2569-0001", "InProgress", new DateTime(2026, 1, 2), "รายละเอียด"));
        await db.SaveChangesAsync();

        var service = CreateService(db, out _, out var webhook);
        const string summary = "การรถไฟแห่งประเทศไทยขอชี้แจงผลการดำเนินการว่า ได้แก้ไขอุปกรณ์เรียบร้อยแล้ว";

        await service.CloseAsync(1, summary, 99);
        db.ChangeTracker.Clear();

        var complaint = await db.Complaints.FindAsync(1);
        var notes = db.ComplaintNotes.Where(note => note.ComplaintId == 1).ToList();
        Assert.NotNull(complaint);
        Assert.Equal("Closed", complaint.Status);
        Assert.NotNull(complaint.ClosedAt);
        Assert.Contains(notes, note => note.NoteType == "StatusChange" && note.Content == "InProgress→Closed");
        Assert.Contains(notes, note => note.NoteType == "PublicReply" && note.Content == summary);
        webhook.Verify(item => item.QueueAsync(
            "complaint.closed", It.IsAny<object>(), null, default), Times.Once);
    }

    [Fact]
    public async Task CloseAsync_FromPending_IsRejected()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ComplaintCategories.Add(new ComplaintCategory { Id = 1, Name = "บริการ", IsActive = true });
        db.Complaints.Add(Complaint(1, "SRT-COMPL-2569-0001", "Pending", new DateTime(2026, 1, 2), "รายละเอียด"));
        await db.SaveChangesAsync();

        var service = CreateService(db, out _);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CloseAsync(1, "สรุปผล", 99));
        Assert.Equal("ปิดงานได้เฉพาะเรื่องที่กำลังดำเนินการ", exception.Message);
    }

    [Fact]
    public async Task UpdateStatusAsync_ResolvedIsNotTreatedAsClosed()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ComplaintCategories.Add(new ComplaintCategory { Id = 1, Name = "บริการ", IsActive = true });
        db.Complaints.Add(Complaint(1, "SRT-COMPL-2569-0001", "InProgress", new DateTime(2026, 1, 2), "รายละเอียด"));
        await db.SaveChangesAsync();

        var service = CreateService(db, out _, out var webhook);
        await service.UpdateStatusAsync(1, "Resolved", 99, null);
        db.ChangeTracker.Clear();

        var complaint = await db.Complaints.FindAsync(1);
        Assert.NotNull(complaint);
        Assert.Equal("Resolved", complaint.Status);
        Assert.Null(complaint.ClosedAt);
        webhook.Verify(item => item.QueueAsync(
            "complaint.status_changed", It.IsAny<object>(), null, default), Times.Once);
    }

    private static Models.Complaint Complaint(int id, string reference, string status, DateTime createdAt, string description) => new()
    {
        Id = id,
        ReferenceNumber = reference,
        ReporterName = "สมชาย",
        ReporterPhone = "0812345678",
        CategoryId = 1,
        Status = status,
        Priority = "Normal",
        Description = description,
        CreatedAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc)
    };
}
