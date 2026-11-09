using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Models;

namespace SRT.Complaint.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<StaffUser> StaffUsers => Set<StaffUser>();
    public DbSet<ComplaintCategory> ComplaintCategories => Set<ComplaintCategory>();
    public DbSet<ComplaintSubCategory> ComplaintSubCategories => Set<ComplaintSubCategory>();
    public DbSet<ComplaintDepartment> ComplaintDepartments => Set<ComplaintDepartment>();
    public DbSet<SlaConfig> SlaConfigs => Set<SlaConfig>();
    public DbSet<Models.Complaint> Complaints => Set<Models.Complaint>();
    public DbSet<ComplaintAttachment> ComplaintAttachments => Set<ComplaintAttachment>();
    public DbSet<ComplaintNote> ComplaintNotes => Set<ComplaintNote>();
    public DbSet<ComplaintAcceptanceEvidence> ComplaintAcceptanceEvidence => Set<ComplaintAcceptanceEvidence>();
    public DbSet<ComplaintTransferLog> ComplaintTransferLogs => Set<ComplaintTransferLog>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<NotificationDeliveryLog> NotificationDeliveryLogs => Set<NotificationDeliveryLog>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<ApiKeyScope> ApiKeyScopes => Set<ApiKeyScope>();
    public DbSet<ApiRequestLog> ApiRequestLogs => Set<ApiRequestLog>();
    public DbSet<ApiIdempotencyRecord> ApiIdempotencyRecords => Set<ApiIdempotencyRecord>();
    public DbSet<Webhook> Webhooks => Set<Webhook>();
    public DbSet<WebhookDeliveryLog> WebhookDeliveryLogs => Set<WebhookDeliveryLog>();
    public DbSet<WebhookOutboxMessage> WebhookOutboxMessages => Set<WebhookOutboxMessage>();
    public DbSet<EdocDocumentLink> EdocDocumentLinks => Set<EdocDocumentLink>();
    public DbSet<EdocAttachmentDelivery> EdocAttachmentDeliveries => Set<EdocAttachmentDelivery>();
    public DbSet<ComplaintTerms> ComplaintTerms => Set<ComplaintTerms>();
    public DbSet<PdfSignerSettings> PdfSignerSettings => Set<PdfSignerSettings>();
    public DbSet<ContentBlock> ContentBlocks => Set<ContentBlock>();
    public DbSet<ExternalSyncLog> ExternalSyncLogs => Set<ExternalSyncLog>();
    public DbSet<TraffySettings> TraffySettings => Set<TraffySettings>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StaffUser>(e =>
        {
            e.ToTable("StaffUsers", "dbo");
            e.HasIndex(x => x.EmployeeCode).IsUnique();
            e.Property(x => x.EmployeeCode).HasMaxLength(7).IsFixedLength();
            e.Property(x => x.FullName).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.PasswordHash).HasMaxLength(512);
            e.Property(x => x.Role).HasMaxLength(50);
        });

        modelBuilder.Entity<ComplaintCategory>(e =>
        {
            e.ToTable("ComplaintCategories", "dbo");
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.DepartmentName).HasMaxLength(200);
            e.Property(x => x.DefaultPriority).HasMaxLength(20);
        });

        modelBuilder.Entity<ComplaintSubCategory>(e =>
        {
            e.ToTable("ComplaintSubCategories", "dbo");
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasOne(x => x.Category)
             .WithMany(c => c.SubCategories)
             .HasForeignKey(x => x.CategoryId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ComplaintDepartment>(e =>
        {
            e.ToTable("ComplaintDepartments", "dbo");
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<SlaConfig>(e =>
        {
            e.ToTable("SlaConfigs", "dbo");
            e.HasIndex(x => x.Priority).IsUnique();
            e.Property(x => x.Priority).HasMaxLength(20);
            e.Property(x => x.LabelTh).HasMaxLength(100);
            e.HasOne(x => x.UpdatedBy).WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Models.Complaint>(e =>
        {
            e.ToTable("Complaints", "dbo");
            e.HasIndex(x => x.ReferenceNumber).IsUnique();
            e.Property(x => x.ReferenceNumber).HasMaxLength(20);
            e.Property(x => x.ReporterName).HasMaxLength(200);
            e.Property(x => x.ReporterPhone).HasMaxLength(20);
            e.Property(x => x.ReporterEmail).HasMaxLength(200);
            e.Property(x => x.ReporterIdCard).HasMaxLength(20);
            e.Property(x => x.ReporterPrivacyChoice).HasMaxLength(20);
            e.Property(x => x.SubmissionPurpose).HasMaxLength(40);
            e.Property(x => x.ResponsibleDepartment).HasMaxLength(200);
            e.Property(x => x.SubjectStation).HasMaxLength(200);
            e.Property(x => x.Channel).HasMaxLength(20).HasDefaultValue("Web");
            e.Property(x => x.Priority).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(50);
            e.Property(x => x.WorkflowStep).HasMaxLength(30).HasDefaultValue(ComplaintWorkflowSteps.Received);
            e.Property(x => x.WorkflowUpdatedAt).HasDefaultValueSql("GETUTCDATE()");
            e.Property(x => x.ResolutionSummary).HasMaxLength(4000);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Property(x => x.SatisfactionNote).HasMaxLength(500);
            e.HasOne(x => x.AssignedTo).WithMany(u => u.AssignedComplaints).HasForeignKey(x => x.AssignedToId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.SubCategory).WithMany(s => s.Complaints).HasForeignKey(x => x.SubCategoryId).OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ComplaintAcceptanceEvidence>(e =>
        {
            e.ToTable("ComplaintAcceptanceEvidence", "dbo");
            e.HasIndex(x => x.EvidenceId).IsUnique();
            e.HasIndex(x => new { x.ComplaintId, x.AcceptanceType }).IsUnique();
            e.Property(x => x.AcceptanceType).HasMaxLength(50);
            e.Property(x => x.TermsTitle).HasMaxLength(300);
            e.Property(x => x.TermsVersion).HasMaxLength(80);
            e.Property(x => x.TermsContentHash).HasMaxLength(64);
            e.Property(x => x.Source).HasMaxLength(100);
            e.HasOne(x => x.Complaint)
                .WithMany(x => x.AcceptanceEvidence)
                .HasForeignKey(x => x.ComplaintId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ComplaintAttachment>(e =>
        {
            e.ToTable("ComplaintAttachments", "dbo");
            e.Property(x => x.FileName).HasMaxLength(500);
            e.Property(x => x.StoredPath).HasMaxLength(1000);
            e.Property(x => x.MimeType).HasMaxLength(100);
            // ClientSetNull (not Cascade) — Complaint already cascades to ComplaintAttachment
            // directly; also cascading via ComplaintNote would give SQL Server two cascade
            // paths to the same table, which it rejects.
            e.HasOne(x => x.Note).WithMany(n => n.Attachments)
             .HasForeignKey(x => x.ComplaintNoteId)
             .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ComplaintNote>(e =>
        {
            e.ToTable("ComplaintNotes", "dbo");
            e.Property(x => x.NoteType).HasMaxLength(20);
            e.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ComplaintTransferLog>(e =>
        {
            e.ToTable("ComplaintTransferLog", "dbo");
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasOne(x => x.FromOfficer).WithMany().HasForeignKey(x => x.FromOfficerId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ToOfficer).WithMany().HasForeignKey(x => x.ToOfficerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs", "dbo");
            e.Property(x => x.ActorCode).HasMaxLength(20);
            e.Property(x => x.Action).HasMaxLength(100);
            e.Property(x => x.EntityType).HasMaxLength(50);
            e.Property(x => x.EntityId).HasMaxLength(50);
            e.Property(x => x.IpAddress).HasMaxLength(50);
            e.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<NotificationTemplate>(e =>
        {
            e.ToTable("NotificationTemplates", "dbo");
            e.HasIndex(x => x.EventKey).IsUnique();
            e.Property(x => x.EventKey).HasMaxLength(100);
            e.Property(x => x.LabelTh).HasMaxLength(200);
            e.Property(x => x.EmailSubject).HasMaxLength(300);
        });

        modelBuilder.Entity<NotificationDeliveryLog>(e =>
        {
            e.ToTable("NotificationDeliveryLogs", "dbo");
            e.HasIndex(x => x.AttemptedAt);
            e.Property(x => x.EventKey).HasMaxLength(100);
            e.Property(x => x.Channel).HasMaxLength(20);
            e.Property(x => x.RecipientMasked).HasMaxLength(200);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.ErrorMessage).HasMaxLength(1000);
        });

        modelBuilder.Entity<Partner>(e =>
        {
            e.ToTable("Partners", "dbo");
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.ContactName).HasMaxLength(200);
            e.Property(x => x.ContactEmail).HasMaxLength(200);
            e.Property(x => x.ContactPhone).HasMaxLength(50);
        });

        modelBuilder.Entity<ApiKey>(e =>
        {
            e.ToTable("ApiKeys", "dbo");
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.KeyType).HasMaxLength(20);
            e.Property(x => x.Environment).HasMaxLength(20);
            e.Property(x => x.DataAccessMode).HasMaxLength(20);
            e.Property(x => x.Purpose).HasMaxLength(500);
            e.Property(x => x.KeyPrefix).HasMaxLength(32);
            e.Property(x => x.KeyHash).HasMaxLength(512);
            e.Property(x => x.RevokedReason).HasMaxLength(500);
            e.HasIndex(x => x.KeyHash).IsUnique();
            e.HasIndex(x => new { x.PartnerId, x.Environment });
            e.HasOne(x => x.Partner).WithMany(x => x.ApiKeys).HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.RevokedBy).WithMany().HasForeignKey(x => x.RevokedById).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ApiKeyScope>(e =>
        {
            e.ToTable("ApiKeyScopes", "dbo");
            e.HasIndex(x => x.ApiKeyId);
            e.Property(x => x.Scope).HasMaxLength(100);
            e.HasOne(x => x.ApiKey).WithMany(k => k.Scopes).HasForeignKey(x => x.ApiKeyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiRequestLog>(e =>
        {
            e.ToTable("ApiRequestLogs", "dbo");
            e.HasIndex(x => new { x.ApiKeyId, x.CreatedAt });
            e.Property(x => x.HttpMethod).HasMaxLength(10);
            e.Property(x => x.Endpoint).HasMaxLength(500);
            e.Property(x => x.QueryString).HasMaxLength(1000);
            e.Property(x => x.IpAddress).HasMaxLength(50);
        });

        modelBuilder.Entity<ApiIdempotencyRecord>(e =>
        {
            e.ToTable("ApiIdempotencyRecords", "dbo");
            e.HasIndex(x => new { x.ApiKeyId, x.IdempotencyKey }).IsUnique();
            e.HasIndex(x => x.ExpiresAt);
            e.Property(x => x.IdempotencyKey).HasMaxLength(128);
            e.Property(x => x.RequestHash).HasMaxLength(64);
            e.Property(x => x.State).HasMaxLength(20);
            e.HasOne(x => x.ApiKey).WithMany(x => x.IdempotencyRecords).HasForeignKey(x => x.ApiKeyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Webhook>(e =>
        {
            e.ToTable("Webhooks", "dbo");
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.TargetUrl).HasMaxLength(1000);
            e.Property(x => x.SecretHash).HasMaxLength(512);
        });

        modelBuilder.Entity<WebhookDeliveryLog>(e =>
        {
            e.ToTable("WebhookDeliveryLogs", "dbo");
            e.Property(x => x.EventType).HasMaxLength(100);
            e.HasIndex(x => x.DeliveryId).IsUnique();
            e.HasIndex(x => new { x.WebhookId, x.OutboxMessageId }).IsUnique().HasFilter("[OutboxMessageId] IS NOT NULL");
            e.HasOne(x => x.OutboxMessage).WithMany(x => x.DeliveryLogs).HasForeignKey(x => x.OutboxMessageId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WebhookOutboxMessage>(e =>
        {
            e.ToTable("WebhookOutboxMessages", "dbo");
            e.HasIndex(x => x.EventId).IsUnique();
            e.HasIndex(x => new { x.ProcessedAt, x.NextAttemptAt });
            e.Property(x => x.EventType).HasMaxLength(100);
            e.Property(x => x.LastError).HasMaxLength(2000);
            e.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EdocDocumentLink>(e =>
        {
            e.ToTable("EdocDocumentLinks", "dbo");
            e.HasIndex(x => x.ComplaintId).IsUnique();
            e.HasIndex(x => x.WID).IsUnique();
            e.HasIndex(x => new { x.SyncStatus, x.NextTrackingAt });
            e.Property(x => x.WID).HasMaxLength(100);
            e.Property(x => x.BasketId).HasMaxLength(13);
            e.Property(x => x.BasketName).HasMaxLength(200);
            e.Property(x => x.RegisterNumber).HasMaxLength(20);
            e.Property(x => x.RemoteDocumentDate).HasMaxLength(50);
            e.Property(x => x.SyncStatus).HasMaxLength(40);
            e.Property(x => x.RemoteStatusCode).HasMaxLength(20);
            e.Property(x => x.RemoteStatusDescription).HasMaxLength(2000);
            e.Property(x => x.LastError).HasMaxLength(2000);
            e.HasOne(x => x.Complaint).WithOne().HasForeignKey<EdocDocumentLink>(x => x.ComplaintId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EdocAttachmentDelivery>(e =>
        {
            e.ToTable("EdocAttachmentDeliveries", "dbo");
            e.HasIndex(x => new { x.EdocDocumentLinkId, x.ComplaintAttachmentId }).IsUnique();
            e.Property(x => x.Status).HasMaxLength(30);
            e.Property(x => x.LastError).HasMaxLength(2000);
            e.HasOne(x => x.EdocDocumentLink).WithMany(x => x.AttachmentDeliveries)
                .HasForeignKey(x => x.EdocDocumentLinkId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ComplaintAttachment).WithMany()
                .HasForeignKey(x => x.ComplaintAttachmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ContentBlock>(e =>
        {
            e.ToTable("ContentBlocks", "dbo");
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Key).HasMaxLength(100);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.ImagePath).HasMaxLength(500);
            e.HasOne(x => x.UpdatedBy)
             .WithMany()
             .HasForeignKey(x => x.UpdatedById)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PdfSignerSettings>(e =>
        {
            e.ToTable("PdfSignerSettings", "dbo");
            e.Property(x => x.FullName).HasMaxLength(200);
            e.Property(x => x.PositionTitle).HasMaxLength(200);
            e.Property(x => x.ActingTitle).HasMaxLength(300);
            e.Property(x => x.SignatureStoredPath).HasMaxLength(1000);
            e.HasOne(x => x.UpdatedBy)
             .WithMany()
             .HasForeignKey(x => x.UpdatedById)
             .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Models.Complaint>(e =>
        {
            e.Property(x => x.ExternalSystem).HasMaxLength(50);
            e.Property(x => x.ExternalId).HasMaxLength(100);
            e.HasIndex(x => new { x.ExternalSystem, x.ExternalId })
             .IsUnique()
             .HasFilter("[ExternalId] IS NOT NULL");
            e.HasIndex(x => x.SourcePartnerId);
            e.HasOne(x => x.SourcePartner).WithMany(x => x.Complaints).HasForeignKey(x => x.SourcePartnerId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ExternalSyncLog>(e =>
        {
            e.ToTable("ExternalSyncLogs", "dbo");
            e.Property(x => x.ExternalSystem).HasMaxLength(50);
            e.Property(x => x.SyncStatus).HasMaxLength(20);
            e.HasOne(x => x.TriggeredBy).WithMany()
             .HasForeignKey(x => x.TriggeredById)
             .OnDelete(DeleteBehavior.ClientSetNull);
        });

        SeedData(modelBuilder);
    }

    private static void SeedData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ComplaintCategory>().HasData(
            new ComplaintCategory { Id = 1, Name = "ความตรงต่อเวลา", DepartmentName = "ฝ่ายการเดินรถ", DefaultPriority = "Normal", SortOrder = 1 },
            new ComplaintCategory { Id = 2, Name = "บริการบนขบวนรถ", DepartmentName = "ฝ่ายการโดยสาร", DefaultPriority = "Normal", SortOrder = 2 },
            new ComplaintCategory { Id = 3, Name = "พนักงาน / มารยาท", DepartmentName = "ฝ่ายบริหารทรัพยากรบุคคล", DefaultPriority = "Normal", SortOrder = 3 },
            new ComplaintCategory { Id = 4, Name = "สิ่งอำนวยความสะดวก", DepartmentName = "ฝ่ายโยธา", DefaultPriority = "Normal", SortOrder = 4 },
            new ComplaintCategory { Id = 5, Name = "ความสะอาด", DepartmentName = "ฝ่ายบริการสถานี", DefaultPriority = "Normal", SortOrder = 5 },
            new ComplaintCategory { Id = 6, Name = "ตั๋ว / การคืนเงิน", DepartmentName = "ฝ่ายการพาณิชย์", DefaultPriority = "High", SortOrder = 6 },
            new ComplaintCategory { Id = 7, Name = "ความปลอดภัย", DepartmentName = "ฝ่ายรักษาความปลอดภัย", DefaultPriority = "Urgent", SortOrder = 7 },
            new ComplaintCategory { Id = 8, Name = "สถานี / ที่จอดรถ", DepartmentName = "ฝ่ายบริการสถานี", DefaultPriority = "Normal", SortOrder = 8 },
            new ComplaintCategory { Id = 9, Name = "อื่น ๆ", DepartmentName = null, DefaultPriority = "Normal", SortOrder = 9 }
        );

        var seedDate = new DateTime(2568 - 543, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<PdfSignerSettings>().HasData(
            new PdfSignerSettings
            {
                Id = 1,
                FullName = "นางสาวชัชชนก แสงมุข",
                PositionTitle = "หัวหน้ากองประชาสัมพันธ์",
                ActingTitle = "ปฏิบัติการแทน ผู้อำนวยการศูนย์ประชาสัมพันธ์",
                UpdatedAt = new DateTime(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc)
            });
        modelBuilder.Entity<SlaConfig>().HasData(
            new SlaConfig { Id = 1, Priority = "Critical", LabelTh = "เร่งด่วนมาก (ความปลอดภัย)", ResolutionHours = 24, AutoAssignAfterHours = 1, UpdatedAt = seedDate },
            new SlaConfig { Id = 2, Priority = "Urgent", LabelTh = "เร่งด่วน", ResolutionHours = 72, AutoAssignAfterHours = 4, UpdatedAt = seedDate },
            new SlaConfig { Id = 3, Priority = "High", LabelTh = "สำคัญ", ResolutionHours = 120, AutoAssignAfterHours = 8, UpdatedAt = seedDate },
            new SlaConfig { Id = 4, Priority = "Normal", LabelTh = "ปกติ", ResolutionHours = 168, AutoAssignAfterHours = 12, UpdatedAt = seedDate },
            new SlaConfig { Id = 5, Priority = "Low", LabelTh = "ข้อเสนอแนะ", ResolutionHours = 360, AutoAssignAfterHours = 24, UpdatedAt = seedDate }
        );

        modelBuilder.Entity<NotificationTemplate>().HasData(
            new NotificationTemplate { Id = 1, EventKey = "ComplaintReceived", LabelTh = "รับเรื่องร้องเรียนแล้ว", EmailSubject = "[รฟท.] รับเรื่องร้องเรียนของท่านแล้ว เลขที่ {ReferenceNumber}", SmsBody = "[รฟท.] รับเรื่องร้องเรียนของท่านแล้ว เลขที่ {ReferenceNumber} ติดตามสถานะ: {TrackingUrl}" },
            new NotificationTemplate { Id = 2, EventKey = "StatusChanged", LabelTh = "สถานะเรื่องเปลี่ยนแปลง", EmailSubject = "[รฟท.] อัปเดตสถานะเรื่องร้องเรียน {ReferenceNumber}", SmsBody = "[รฟท.] เรื่อง {ReferenceNumber} อัปเดตสถานะเป็น: {Status}" },
            new NotificationTemplate { Id = 3, EventKey = "ComplaintClosed", LabelTh = "ปิดเรื่องร้องเรียนแล้ว", EmailSubject = "[รฟท.] เรื่องร้องเรียน {ReferenceNumber} ได้รับการแก้ไขแล้ว", SmsBody = "[รฟท.] เรื่อง {ReferenceNumber} ปิดแล้ว ขอบคุณที่ใช้บริการ" },
            new NotificationTemplate { Id = 4, EventKey = "AutoAssigned", LabelTh = "มอบหมายเรื่องอัตโนมัติ", EmailSubject = "[รฟท.] มอบหมายเรื่องร้องเรียนให้ท่าน {ReferenceNumber}", SmsBody = null, IsSmsEnabled = false },
            new NotificationTemplate { Id = 5, EventKey = "SlaWarning", LabelTh = "เตือน SLA ใกล้ครบกำหนด", EmailSubject = "[รฟท.] เตือน: เรื่อง {ReferenceNumber} ใกล้ครบกำหนด SLA", SmsBody = null, IsSmsEnabled = false },
            new NotificationTemplate { Id = 6, EventKey = "SlaBreached", LabelTh = "SLA เกินกำหนดแล้ว", EmailSubject = "[รฟท.] แจ้งเตือน: เรื่อง {ReferenceNumber} เกิน SLA แล้ว", SmsBody = null, IsSmsEnabled = false },
            new NotificationTemplate { Id = 7, EventKey = "CriticalReceived", LabelTh = "รับเรื่องเร่งด่วนมาก", EmailSubject = "[รฟท.] เร่งด่วน: รับเรื่องร้องเรียนระดับ Critical {ReferenceNumber}", SmsBody = null, IsSmsEnabled = false },
            new NotificationTemplate { Id = 8, EventKey = "DecryptionRequested", LabelTh = "ขอดูข้อมูลผู้แจ้งทุจริต", EmailSubject = "[รฟท.] แจ้งเตือน: มีการขอดูข้อมูลผู้แจ้ง {ReferenceNumber}", SmsBody = null, IsSmsEnabled = false }
        );

        modelBuilder.Entity<ComplaintSubCategory>().HasData(
            // 1: ความตรงต่อเวลา
            new ComplaintSubCategory { Id =  1, CategoryId = 1, Name = "รถออกล่าช้า",                              IsActive = true, SortOrder = 1 },
            new ComplaintSubCategory { Id =  2, CategoryId = 1, Name = "รถมาถึงล่าช้า",                           IsActive = true, SortOrder = 2 },
            new ComplaintSubCategory { Id =  3, CategoryId = 1, Name = "ยกเลิกขบวนโดยไม่แจ้งล่วงหน้า",           IsActive = true, SortOrder = 3 },
            new ComplaintSubCategory { Id =  4, CategoryId = 1, Name = "หยุดกลางทางนานผิดปกติ",                  IsActive = true, SortOrder = 4 },
            new ComplaintSubCategory { Id =  5, CategoryId = 1, Name = "เวลาจอดสถานีไม่ตรงตารางเวลา",            IsActive = true, SortOrder = 5 },
            // 2: บริการบนขบวนรถ
            new ComplaintSubCategory { Id =  6, CategoryId = 2, Name = "ที่นั่ง / เตียงชำรุด",                   IsActive = true, SortOrder = 1 },
            new ComplaintSubCategory { Id =  7, CategoryId = 2, Name = "แอร์ / พัดลมไม่ทำงาน",                   IsActive = true, SortOrder = 2 },
            new ComplaintSubCategory { Id =  8, CategoryId = 2, Name = "ห้องน้ำบนขบวนชำรุด / สกปรก",             IsActive = true, SortOrder = 3 },
            new ComplaintSubCategory { Id =  9, CategoryId = 2, Name = "บริการอาหารและเครื่องดื่ม",               IsActive = true, SortOrder = 4 },
            new ComplaintSubCategory { Id = 10, CategoryId = 2, Name = "เต้ารับไฟฟ้าชำรุด",                       IsActive = true, SortOrder = 5 },
            new ComplaintSubCategory { Id = 11, CategoryId = 2, Name = "Wi-Fi / ระบบบันเทิงขัดข้อง",              IsActive = true, SortOrder = 6 },
            // 3: พนักงาน / มารยาท
            new ComplaintSubCategory { Id = 12, CategoryId = 3, Name = "พูดจาไม่สุภาพ / ไม่ให้เกียรติ",          IsActive = true, SortOrder = 1 },
            new ComplaintSubCategory { Id = 13, CategoryId = 3, Name = "บริการล่าช้า / ไม่ใส่ใจ",                IsActive = true, SortOrder = 2 },
            new ComplaintSubCategory { Id = 14, CategoryId = 3, Name = "ให้ข้อมูลผิด / ไม่ถูกต้อง",              IsActive = true, SortOrder = 3 },
            new ComplaintSubCategory { Id = 15, CategoryId = 3, Name = "พฤติกรรมไม่เหมาะสม",                     IsActive = true, SortOrder = 4 },
            new ComplaintSubCategory { Id = 16, CategoryId = 3, Name = "เลือกปฏิบัติ",                            IsActive = true, SortOrder = 5 },
            // 4: สิ่งอำนวยความสะดวก
            new ComplaintSubCategory { Id = 17, CategoryId = 4, Name = "ลิฟต์ / บันไดเลื่อนขัดข้อง",             IsActive = true, SortOrder = 1 },
            new ComplaintSubCategory { Id = 18, CategoryId = 4, Name = "ที่นั่งรอในสถานีชำรุด",                   IsActive = true, SortOrder = 2 },
            new ComplaintSubCategory { Id = 19, CategoryId = 4, Name = "สิ่งอำนวยความสะดวกสำหรับผู้พิการ",        IsActive = true, SortOrder = 3 },
            new ComplaintSubCategory { Id = 20, CategoryId = 4, Name = "ป้ายบอกทาง / ข้อมูลไม่ชัดเจน",           IsActive = true, SortOrder = 4 },
            new ComplaintSubCategory { Id = 21, CategoryId = 4, Name = "ระบบประกาศเสียงขัดข้อง",                  IsActive = true, SortOrder = 5 },
            // 5: ความสะอาด
            new ComplaintSubCategory { Id = 22, CategoryId = 5, Name = "ความสะอาดในขบวนรถ",                       IsActive = true, SortOrder = 1 },
            new ComplaintSubCategory { Id = 23, CategoryId = 5, Name = "ความสะอาดในสถานี",                        IsActive = true, SortOrder = 2 },
            new ComplaintSubCategory { Id = 24, CategoryId = 5, Name = "ห้องน้ำสกปรก",                            IsActive = true, SortOrder = 3 },
            new ComplaintSubCategory { Id = 25, CategoryId = 5, Name = "การกำจัดขยะ",                             IsActive = true, SortOrder = 4 },
            // 6: ตั๋ว / การคืนเงิน
            new ComplaintSubCategory { Id = 26, CategoryId = 6, Name = "ปัญหาการจองตั๋ว",                        IsActive = true, SortOrder = 1 },
            new ComplaintSubCategory { Id = 27, CategoryId = 6, Name = "ขอคืนเงินค่าตั๋ว",                       IsActive = true, SortOrder = 2 },
            new ComplaintSubCategory { Id = 28, CategoryId = 6, Name = "ราคาตั๋วไม่ถูกต้อง",                      IsActive = true, SortOrder = 3 },
            new ComplaintSubCategory { Id = 29, CategoryId = 6, Name = "ระบบออนไลน์ขัดข้อง",                     IsActive = true, SortOrder = 4 },
            new ComplaintSubCategory { Id = 30, CategoryId = 6, Name = "ปัญหาโปรโมชั่น / ส่วนลด",                IsActive = true, SortOrder = 5 },
            // 7: ความปลอดภัย
            new ComplaintSubCategory { Id = 31, CategoryId = 7, Name = "การโจรกรรม / ทรัพย์สินสูญหาย",           IsActive = true, SortOrder = 1 },
            new ComplaintSubCategory { Id = 32, CategoryId = 7, Name = "การล่วงละเมิด / คุกคาม",                  IsActive = true, SortOrder = 2 },
            new ComplaintSubCategory { Id = 33, CategoryId = 7, Name = "ผู้โดยสารก่อกวน / พฤติกรรมน่ารังเกียจ",  IsActive = true, SortOrder = 3 },
            new ComplaintSubCategory { Id = 34, CategoryId = 7, Name = "อุบัติเหตุในบริเวณสถานี",                 IsActive = true, SortOrder = 4 },
            new ComplaintSubCategory { Id = 35, CategoryId = 7, Name = "การลักลอบนำสิ่งของอันตราย",               IsActive = true, SortOrder = 5 },
            // 8: สถานี / ที่จอดรถ
            new ComplaintSubCategory { Id = 36, CategoryId = 8, Name = "ที่จอดรถไม่เพียงพอ",                     IsActive = true, SortOrder = 1 },
            new ComplaintSubCategory { Id = 37, CategoryId = 8, Name = "ค่าจอดรถไม่ถูกต้อง",                     IsActive = true, SortOrder = 2 },
            new ComplaintSubCategory { Id = 38, CategoryId = 8, Name = "สิ่งก่อสร้างกีดขวาง / ทางเข้าออก",       IsActive = true, SortOrder = 3 },
            new ComplaintSubCategory { Id = 39, CategoryId = 8, Name = "ไฟส่องสว่างไม่เพียงพอ",                   IsActive = true, SortOrder = 4 },
            // 9: อื่น ๆ
            new ComplaintSubCategory { Id = 40, CategoryId = 9, Name = "ข้อเสนอแนะทั่วไป",                        IsActive = true, SortOrder = 1 },
            new ComplaintSubCategory { Id = 41, CategoryId = 9, Name = "ไม่อยู่ในหมวดหมู่ข้างต้น",               IsActive = true, SortOrder = 2 }
        );

        modelBuilder.Entity<ComplaintTerms>(e =>
        {
            e.ToTable("ComplaintTerms", "dbo");
            e.Property(x => x.Title).HasMaxLength(300);
            e.HasOne(x => x.UpdatedBy)
             .WithMany()
             .HasForeignKey(x => x.UpdatedById)
             .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<TraffySettings>(e =>
        {
            e.ToTable("TraffySettings", "dbo");
            e.Property(x => x.ApiUrl).HasMaxLength(300);
            e.Property(x => x.Username).HasMaxLength(200);
            e.Property(x => x.OrgId).HasMaxLength(200);
            e.HasOne(x => x.UpdatedBy)
             .WithMany()
             .HasForeignKey(x => x.UpdatedById)
             .OnDelete(DeleteBehavior.ClientSetNull);
        });
    }
}
