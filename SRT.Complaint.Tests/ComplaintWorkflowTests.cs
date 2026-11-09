using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SRT.Complaint.Models;
using SRT.Complaint.Services;

namespace SRT.Complaint.Tests;

public class ComplaintWorkflowTests
{
    [Fact]
    public async Task GuidedWorkflow_RequiresEachStepAndCompletesInOrder()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ComplaintCategories.Add(new ComplaintCategory
        {
            Id = 1,
            Name = "บริการ",
            DepartmentName = "ฝ่ายบริการ",
            IsActive = true
        });
        db.Complaints.Add(new Models.Complaint
        {
            Id = 1,
            ReferenceNumber = "SRT-COMPL-2569-0001",
            ReporterName = "ผู้ร้องทดสอบ",
            ReporterPhone = "0812345678",
            CategoryId = 1,
            ResponsibleDepartment = "ฝ่ายบริการ",
            Description = "รายละเอียดสำหรับทดสอบกระบวนการ",
            AssignedToId = 99,
            Status = "InProgress",
            WorkflowStep = ComplaintWorkflowSteps.Screening,
            Priority = "Normal",
            SlaDeadline = DateTime.UtcNow.AddDays(3)
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.CompleteScreeningAsync(1, true, null, 99);
        Assert.Equal(ComplaintWorkflowSteps.Classification, (await db.Complaints.FindAsync(1))!.WorkflowStep);

        await service.CompleteClassificationAsync(1, 99);
        Assert.Equal(ComplaintWorkflowSteps.Investigation, (await db.Complaints.FindAsync(1))!.WorkflowStep);

        var missingWork = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CompleteInvestigationAsync(1, "ตรวจสอบข้อเท็จจริงและดำเนินการครบถ้วนแล้ว", "ไม่มีเอกสารจากหน่วยงาน", 99));
        Assert.Contains("บันทึกการดำเนินการ", missingWork.Message);

        db.ComplaintNotes.Add(new ComplaintNote
        {
            ComplaintId = 1,
            AuthorId = 99,
            NoteType = "InternalNote",
            Content = "ลงพื้นที่ตรวจสอบและประสานหน่วยงานแล้ว"
        });
        await db.SaveChangesAsync();

        await service.CompleteInvestigationAsync(
            1,
            "ตรวจสอบข้อเท็จจริงและดำเนินการแก้ไขครบถ้วนแล้ว",
            "หน่วยงานยืนยันผลผ่านระบบภายในโดยไม่มีเอกสารแนบ",
            99);
        Assert.Equal(ComplaintWorkflowSteps.Summary, (await db.Complaints.FindAsync(1))!.WorkflowStep);

        const string summary = "การรถไฟแห่งประเทศไทยได้ตรวจสอบและแก้ไขปัญหาเรียบร้อยแล้ว";
        await service.PrepareClosureAsync(1, summary, 99);
        Assert.Equal(ComplaintWorkflowSteps.Notification, (await db.Complaints.FindAsync(1))!.WorkflowStep);

        await service.ClosePreparedAsync(1, 99);
        var completed = await db.Complaints.FindAsync(1);
        Assert.Equal("Closed", completed!.Status);
        Assert.Equal(ComplaintWorkflowSteps.Completed, completed.WorkflowStep);
    }

    [Fact]
    public async Task AcceptanceEvidence_IsStoredWithComplaintWithoutExtraDeviceData()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ComplaintCategories.Add(new ComplaintCategory { Id = 1, Name = "บริการ", IsActive = true });
        var complaint = new Models.Complaint
        {
            ReferenceNumber = "SRT-COMPL-2569-0002",
            ReporterName = "ผู้ร้องทดสอบ",
            ReporterPhone = "0812345678",
            CategoryId = 1,
            Description = "รายละเอียดสำหรับทดสอบหลักฐาน",
            Priority = "Normal"
        };
        complaint.AcceptanceEvidence.Add(new ComplaintAcceptanceEvidence
        {
            EvidenceId = Guid.NewGuid(),
            TermsTitle = "หลักเกณฑ์",
            TermsVersion = "2026-10-30T00:00:00.0000000Z",
            TermsContentHash = new string('A', 64),
            TermsContentSnapshot = "<p>ข้อความที่ยอมรับ</p>",
            Source = "PublicComplaintForm",
            AcceptedAt = DateTime.UtcNow
        });
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();

        var evidence = Assert.Single(db.ComplaintAcceptanceEvidence);
        Assert.Equal(complaint.Id, evidence.ComplaintId);
        Assert.Equal("ComplaintTerms", evidence.AcceptanceType);
        Assert.NotEqual(Guid.Empty, evidence.EvidenceId);
    }

    [Theory]
    [InlineData(ComplaintWorkflowSteps.Classification, ComplaintWorkflowSteps.Screening)]
    [InlineData(ComplaintWorkflowSteps.Investigation, ComplaintWorkflowSteps.Classification)]
    [InlineData(ComplaintWorkflowSteps.Summary, ComplaintWorkflowSteps.Investigation)]
    [InlineData(ComplaintWorkflowSteps.Notification, ComplaintWorkflowSteps.Summary)]
    public async Task MoveToPreviousWorkflowStep_MovesBackOneStepWithoutRemovingExistingData(
        string currentStep,
        string expectedStep)
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ComplaintCategories.Add(new ComplaintCategory { Id = 1, Name = "บริการ", IsActive = true });
        db.Complaints.Add(new Models.Complaint
        {
            Id = 10,
            ReferenceNumber = "SRT-COMPL-2569-0010",
            ReporterName = "ผู้ร้องทดสอบ",
            ReporterPhone = "0812345678",
            CategoryId = 1,
            Description = "รายละเอียดเรื่องร้องเรียนสำหรับทดสอบการย้อนขั้นตอน",
            AssignedToId = 99,
            Status = "InProgress",
            WorkflowStep = currentStep,
            ResolutionSummary = "ข้อความสรุปผลเดิมที่ต้องเก็บไว้",
            Priority = "Normal"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.MoveToPreviousWorkflowStepAsync(10, 99);

        var complaint = await db.Complaints.FindAsync(10);
        Assert.Equal(expectedStep, complaint!.WorkflowStep);
        Assert.Equal("ข้อความสรุปผลเดิมที่ต้องเก็บไว้", complaint.ResolutionSummary);
        Assert.Contains(db.ComplaintNotes, note =>
            note.ComplaintId == 10 && note.NoteType == "Workflow");
    }

    [Fact]
    public async Task ReopenAsync_ReturnsClosedComplaintToScreeningStep()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ComplaintCategories.Add(new ComplaintCategory { Id = 1, Name = "บริการ", IsActive = true });
        db.Complaints.Add(new Models.Complaint
        {
            Id = 11,
            ReferenceNumber = "SRT-COMPL-2569-0011",
            ReporterName = "ผู้ร้องทดสอบ",
            ReporterPhone = "0812345678",
            CategoryId = 1,
            Description = "รายละเอียดเรื่องร้องเรียนสำหรับทดสอบการเปิดเรื่องใหม่",
            AssignedToId = 99,
            Status = "Closed",
            WorkflowStep = ComplaintWorkflowSteps.Completed,
            ClosedAt = DateTime.UtcNow,
            Priority = "Normal"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.ReopenAsync(11, 99);

        var complaint = await db.Complaints.FindAsync(11);
        Assert.Equal("InProgress", complaint!.Status);
        Assert.Equal(ComplaintWorkflowSteps.Screening, complaint.WorkflowStep);
        Assert.Null(complaint.ClosedAt);
    }

    [Fact]
    public async Task RequestMoreInfo_AndReporterReply_CompleteThePublicResponseLoop()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ComplaintCategories.Add(new ComplaintCategory { Id = 1, Name = "บริการ", IsActive = true });
        db.Complaints.Add(new Models.Complaint
        {
            Id = 12,
            ReferenceNumber = "SRT-COMPL-2569-0012",
            ReporterName = "ผู้ร้องทดสอบ",
            ReporterPhone = "0812345678",
            ReporterEmail = "reporter@example.com",
            CategoryId = 1,
            Description = "รายละเอียดเรื่องร้องเรียนสำหรับทดสอบการขอข้อมูลเพิ่ม",
            AssignedToId = 99,
            Status = "InProgress",
            WorkflowStep = ComplaintWorkflowSteps.Screening,
            Priority = "Normal"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.CompleteScreeningAsync(12, false, "กรุณาแนบภาพถ่ายเพิ่มเติม", 99);

        var waiting = await db.Complaints.FindAsync(12);
        Assert.Equal("WaitingInfo", waiting!.Status);
        Assert.Equal(ComplaintWorkflowSteps.WaitingInfo, waiting.WorkflowStep);
        Assert.Contains(db.ComplaintNotes, note =>
            note.ComplaintId == 12 && note.NoteType == "InfoRequest" &&
            note.Content == "กรุณาแนบภาพถ่ายเพิ่มเติม");

        await service.SubmitReporterReplyAsync(
            waiting.ReferenceNumber,
            "แนบรายละเอียดและยืนยันสถานที่เกิดเหตุแล้ว");

        Assert.Contains(db.ComplaintNotes, note =>
            note.ComplaintId == 12 && note.NoteType == "ReporterReply" && note.AuthorId == null);

        var queue = await service.GetQueueAsync(new ComplaintQueueFilter(QuickView: "reporter-replied"));
        Assert.Contains(queue, complaint => complaint.Id == 12);
    }

    [Fact]
    public async Task ReporterReply_IsRejectedWhenComplaintIsNotWaitingForInformation()
    {
        await using var db = TestHelpers.CreateAppDbContext();
        db.ComplaintCategories.Add(new ComplaintCategory { Id = 1, Name = "บริการ", IsActive = true });
        db.Complaints.Add(new Models.Complaint
        {
            Id = 13,
            ReferenceNumber = "SRT-COMPL-2569-0013",
            ReporterName = "ผู้ร้องทดสอบ",
            ReporterPhone = "0812345678",
            CategoryId = 1,
            Description = "รายละเอียดเรื่องร้องเรียนสำหรับทดสอบสิทธิ์การตอบกลับ",
            AssignedToId = 99,
            Status = "InProgress",
            WorkflowStep = ComplaintWorkflowSteps.Investigation,
            Priority = "Normal"
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SubmitReporterReplyAsync("SRT-COMPL-2569-0013", "ข้อมูลที่ไม่ควรถูกบันทึก"));

        Assert.Contains("ไม่ได้อยู่ระหว่างรอข้อมูลเพิ่มเติม", exception.Message);
        Assert.DoesNotContain(db.ComplaintNotes, note => note.NoteType == "ReporterReply");
    }

    private static ComplaintService CreateService(Data.AppDbContext db) =>
        new(
            db,
            Mock.Of<ISlaService>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IAuditService>(),
            Mock.Of<IWebhookService>(),
            Mock.Of<IServiceScopeFactory>(),
            new ConfigurationBuilder().AddInMemoryCollection().Build());
}
