using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public class ComplaintService(
    AppDbContext db,
    ISlaService slaService,
    INotificationService notificationService,
    IAuditService auditService,
    IWebhookService webhookService,
    IServiceScopeFactory scopeFactory,
    IConfiguration config) : IComplaintService
{
    public async Task<Models.Complaint> SubmitAsync(SubmitComplaintRequest request, CancellationToken ct = default)
    {
        var category = await db.ComplaintCategories.FindAsync([request.CategoryId], ct)
            ?? throw new InvalidOperationException("Category not found");

        var priority = category.DefaultPriority;
        var now = DateTime.UtcNow;
        var refNum = await GenerateReferenceNumberAsync(ct);

        var complaint = new Models.Complaint
        {
            ReferenceNumber = refNum,
            ReporterName = request.ReporterName,
            ReporterPhone = request.ReporterPhone,
            ReporterEmail = request.ReporterEmail,
            ReporterIdCard = request.ReporterIdCard,
            ReporterPrivacyChoice = ReporterPrivacyChoices.IsValid(request.ReporterPrivacyChoice)
                ? request.ReporterPrivacyChoice
                : null,
            SubmissionPurpose = ComplaintSubmissionPurposes.IsValid(request.SubmissionPurpose)
                ? request.SubmissionPurpose
                : null,
            CategoryId = request.CategoryId,
            SubCategoryId = request.SubCategoryId,
            ResponsibleDepartment = category.DepartmentName,
            SubjectStation = request.SubjectStation,
            IncidentDate = request.IncidentDate,
            Description = request.Description,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Channel = request.Channel,
            SourcePartnerId = request.SourcePartnerId,
            ExternalSystem = request.ExternalSystem,
            ExternalId = request.ExternalId,
            Priority = priority,
            Status = "Pending",
            WorkflowStep = ComplaintWorkflowSteps.Received,
            WorkflowUpdatedAt = now,
            SlaDeadline = await slaService.CalculateDeadlineAsync(priority, now),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Complaints.Add(complaint);
        if (request.AcceptanceEvidence is not null)
        {
            complaint.AcceptanceEvidence.Add(new ComplaintAcceptanceEvidence
            {
                AcceptanceType = "ComplaintTerms",
                TermsTitle = request.AcceptanceEvidence.TermsTitle,
                TermsVersion = request.AcceptanceEvidence.TermsVersion,
                TermsContentHash = request.AcceptanceEvidence.TermsContentHash,
                TermsContentSnapshot = request.AcceptanceEvidence.TermsContentSnapshot,
                Source = request.AcceptanceEvidence.Source,
                AcceptedAt = request.AcceptanceEvidence.AcceptedAt
            });
        }
        await db.SaveChangesAsync(ct);
        var createdPayload = new { referenceNumber = refNum, externalReference = complaint.ExternalId, status = complaint.Status, priority = complaint.Priority, createdAt = complaint.CreatedAt };
        await webhookService.QueueAsync("complaint.created", createdPayload, complaint.SourcePartnerId, ct);

        await SaveAttachmentsAsync(complaint.Id, request.Attachments, ct);
        await auditService.LogAsync("ComplaintSubmitted", null, null, "Complaint", complaint.Id.ToString(), new { refNum }, null, ct);

        await notificationService.SendAsync("ComplaintReceived", request.ReporterPhone, request.ReporterEmail, new()
        {
            ["ReferenceNumber"] = refNum,
            ["TrackingUrl"] = $"https://www.railway.co.th/complaint/track/{refNum}"
        }, ct);

        if (priority == "Critical")
            await NotifyStaffOfCriticalAsync(refNum, ct);

        var discordPayload = new NewComplaintDiscordPayload(refNum, category.Name, complaint.Priority, complaint.SubjectStation, complaint.CreatedAt);
        _ = Task.Run(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var discord = scope.ServiceProvider.GetRequiredService<IDiscordNotifier>();
            await discord.NotifyNewComplaintAsync(discordPayload);
        }, CancellationToken.None);

        return complaint;
    }

    public async Task<Models.Complaint?> GetByReferenceAsync(string referenceNumber, CancellationToken ct = default)
        => await db.Complaints
            .Include(c => c.Category)
            .Include(c => c.SubCategory)
            .Include(c => c.AssignedTo)
            .Include(c => c.Attachments)
            .Include(c => c.Notes).ThenInclude(n => n.Attachments)
            .Include(c => c.AcceptanceEvidence)
            .FirstOrDefaultAsync(c => c.ReferenceNumber == referenceNumber, ct);

    public async Task<Models.Complaint?> GetByIdAsync(int id, CancellationToken ct = default)
        => await db.Complaints
            .Include(c => c.Category)
            .Include(c => c.SubCategory)
            .Include(c => c.AssignedTo)
            .Include(c => c.Attachments)
            .Include(c => c.Notes).ThenInclude(n => n.Author)
            .Include(c => c.Notes).ThenInclude(n => n.Attachments)
            .Include(c => c.TransferLogs)
            .Include(c => c.AcceptanceEvidence)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task UpdateStatusAsync(
        int id, string newStatus, int actorId, string? note, CancellationToken ct = default) =>
        UpdateStatusCoreAsync(id, newStatus, actorId, null, note, null, null, ct);

    public Task UpdateStatusFromApiAsync(
        int id,
        string newStatus,
        ApiKey apiKey,
        string? note,
        string? ipAddress,
        CancellationToken ct = default) =>
        UpdateStatusCoreAsync(
            id,
            newStatus,
            null,
            $"APIKEY:{apiKey.KeyPrefix}",
            note,
            new { apiKeyId = apiKey.Id, apiKey.PartnerId, apiKey.KeyType },
            ipAddress,
            ct);

    private async Task UpdateStatusCoreAsync(
        int id,
        string newStatus,
        int? actorId,
        string? actorCode,
        string? note,
        object? apiActor,
        string? ipAddress,
        CancellationToken ct)
    {
        var complaint = await db.Complaints.FindAsync([id], ct)
            ?? throw new InvalidOperationException("Complaint not found");
        if (!ComplaintStatusTransitionPolicy.CanSetStatus(complaint.WorkflowStep, newStatus))
            throw new InvalidOperationException(
                $"Status {newStatus} is not allowed while workflow is {complaint.WorkflowStep}");
        if (ComplaintStatusTransitionPolicy.RequiresNote(newStatus) && string.IsNullOrWhiteSpace(note))
            throw new InvalidOperationException($"A note is required when changing status to {newStatus}");
        if (complaint.Status == newStatus)
            return;

        var now = DateTime.UtcNow;
        var oldStatus = complaint.Status;
        var oldWorkflowStep = complaint.WorkflowStep;
        var nextWorkflowStep = ComplaintStatusTransitionPolicy.NextWorkflowStep(oldWorkflowStep, newStatus);
        complaint.Status = newStatus;
        complaint.UpdatedAt = now;
        if (nextWorkflowStep != oldWorkflowStep)
        {
            complaint.WorkflowStep = nextWorkflowStep;
            complaint.WorkflowUpdatedAt = now;
            db.ComplaintNotes.Add(new ComplaintNote
            {
                ComplaintId = id,
                AuthorId = actorId,
                NoteType = "Workflow",
                Content = $"{ComplaintWorkflowSteps.ThaiLabel(oldWorkflowStep)} → {ComplaintWorkflowSteps.ThaiLabel(nextWorkflowStep)}",
                CreatedAt = now
            });
        }

        if (newStatus is "Closed" or "Rejected")
            complaint.ClosedAt = now;

        db.ComplaintNotes.Add(new ComplaintNote
        {
            ComplaintId = id,
            AuthorId = actorId,
            NoteType = "StatusChange",
            Content = $"{oldStatus}→{newStatus}",
            CreatedAt = now
        });
        if (!string.IsNullOrWhiteSpace(note))
        {
            db.ComplaintNotes.Add(new ComplaintNote
            {
                ComplaintId = id,
                AuthorId = actorId,
                NoteType = "PublicReply",
                Content = note.Trim(),
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(ct);
        var webhookEvent = newStatus == "Closed" ? "complaint.closed" : "complaint.status_changed";
        var statusPayload = new
        {
            referenceNumber = complaint.ReferenceNumber,
            externalReference = complaint.ExternalId,
            oldStatus,
            newStatus,
            workflowStep = complaint.WorkflowStep,
            updatedAt = complaint.UpdatedAt
        };
        await webhookService.QueueAsync(webhookEvent, statusPayload, complaint.SourcePartnerId, ct);
        await auditService.LogAsync(
            "UpdateStatus",
            actorId,
            actorCode,
            "Complaint",
            id.ToString(),
            new { oldStatus, newStatus, oldWorkflowStep, workflowStep = complaint.WorkflowStep, apiActor },
            ipAddress,
            ct);

        await notificationService.SendAsync("StatusChanged", complaint.ReporterPhone, complaint.ReporterEmail, new()
        {
            ["ReferenceNumber"] = complaint.ReferenceNumber,
            ["Status"] = ComplaintStatusLabels.Thai(newStatus)
        }, ct);
    }

    public async Task ClaimAsync(int id, int staffId, CancellationToken ct = default)
    {
        var complaint = await db.Complaints.FindAsync([id], ct)
            ?? throw new InvalidOperationException("Complaint not found");
        if (complaint.AssignedToId.HasValue) throw new InvalidOperationException("Already claimed");

        complaint.AssignedToId = staffId;
        complaint.AssignedAt = DateTime.UtcNow;
        complaint.Status = "InProgress";
        complaint.UpdatedAt = DateTime.UtcNow;
        db.ComplaintNotes.Add(new ComplaintNote
        {
            ComplaintId = id, AuthorId = staffId, NoteType = "StatusChange",
            Content = "Pending→InProgress", CreatedAt = DateTime.UtcNow
        });
        await RecordWorkflowTransitionAsync(complaint, ComplaintWorkflowSteps.Screening, staffId, "รับเรื่องและเริ่มตรวจสอบข้อมูล", ct);
        await auditService.LogAsync("ClaimCase", staffId, null, "Complaint", id.ToString(), null, null, ct);
    }

    public async Task TransferAsync(int id, int toOfficerId, string reason, int actorId, CancellationToken ct = default)
    {
        var complaint = await db.Complaints.FindAsync([id], ct)
            ?? throw new InvalidOperationException("Complaint not found");

        db.ComplaintTransferLogs.Add(new ComplaintTransferLog
        {
            ComplaintId = id,
            FromOfficerId = complaint.AssignedToId,
            ToOfficerId = toOfficerId,
            Reason = reason,
            TransferredAt = DateTime.UtcNow
        });

        complaint.AssignedToId = toOfficerId;
        complaint.AssignedAt = DateTime.UtcNow;
        complaint.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await auditService.LogAsync("TransferCase", actorId, null, "Complaint", id.ToString(), new { toOfficerId, reason }, null, ct);
    }

    public async Task UpdateClassificationAsync(int id, int categoryId, int? subCategoryId,
        string? responsibleDepartment, int actorId, CancellationToken ct = default)
    {
        var complaint = await db.Complaints.FindAsync([id], ct)
            ?? throw new InvalidOperationException("Complaint not found");
        var category = await db.ComplaintCategories
            .FirstOrDefaultAsync(c => c.Id == categoryId && c.IsActive, ct)
            ?? throw new InvalidOperationException("Category not found");

        if (subCategoryId.HasValue)
        {
            var validSubCategory = await db.ComplaintSubCategories.AnyAsync(s =>
                s.Id == subCategoryId.Value && s.CategoryId == categoryId && s.IsActive, ct);
            if (!validSubCategory)
                throw new InvalidOperationException("Subcategory does not belong to the selected category");
        }

        var department = string.IsNullOrWhiteSpace(responsibleDepartment)
            ? category.DepartmentName
            : responsibleDepartment.Trim();
        if (!string.IsNullOrWhiteSpace(department))
        {
            var validDepartment = await db.ComplaintCategories.AnyAsync(c =>
                c.IsActive && c.DepartmentName == department, ct);
            if (!validDepartment)
                throw new InvalidOperationException("Department not found");
        }

        var oldCategoryId = complaint.CategoryId;
        var oldSubCategoryId = complaint.SubCategoryId;
        var oldDepartment = complaint.ResponsibleDepartment;
        complaint.CategoryId = categoryId;
        complaint.SubCategoryId = subCategoryId;
        complaint.ResponsibleDepartment = department;
        complaint.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await auditService.LogAsync("UpdateComplaintClassification", actorId, null, "Complaint", id.ToString(), new
        {
            oldCategoryId,
            categoryId,
            oldSubCategoryId,
            subCategoryId,
            oldDepartment,
            responsibleDepartment = department
        }, null, ct);
    }

    public async Task CompleteScreeningAsync(int id, bool informationComplete, string? reason,
        int actorId, CancellationToken ct = default)
    {
        var complaint = await db.Complaints.FindAsync([id], ct)
            ?? throw new InvalidOperationException("ไม่พบเรื่องร้องเรียน");
        if (complaint.WorkflowStep is not (ComplaintWorkflowSteps.Screening or ComplaintWorkflowSteps.WaitingInfo))
            throw new InvalidOperationException("ต้องอยู่ในขั้นตรวจสอบความครบถ้วนก่อน");

        if (!informationComplete)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("กรุณาระบุข้อมูลที่ต้องการเพิ่มเติม");

            var requestText = reason.Trim();
            db.ComplaintNotes.Add(new ComplaintNote
            {
                ComplaintId = complaint.Id,
                AuthorId = actorId,
                NoteType = "InfoRequest",
                Content = requestText,
                CreatedAt = DateTime.UtcNow
            });

            await RecordWorkflowTransitionAsync(
                complaint,
                ComplaintWorkflowSteps.WaitingInfo,
                actorId,
                $"ขอข้อมูลเพิ่มเติม: {requestText}",
                ct);

            await notificationService.SendAsync("StatusChanged", complaint.ReporterPhone, complaint.ReporterEmail, new()
            {
                ["ReferenceNumber"] = complaint.ReferenceNumber,
                ["Status"] = ComplaintStatusLabels.Thai("WaitingInfo"),
                ["TrackingUrl"] = $"https://www.railway.co.th/complaint/track/{complaint.ReferenceNumber}"
            }, ct);
            return;
        }

        await RecordWorkflowTransitionAsync(
            complaint,
            ComplaintWorkflowSteps.Classification,
            actorId,
            "ตรวจสอบแล้วว่าข้อมูลครบถ้วน",
            ct);
    }

    public async Task ResumeScreeningAsync(int id, int actorId, CancellationToken ct = default)
    {
        var complaint = await db.Complaints.FindAsync([id], ct)
            ?? throw new InvalidOperationException("ไม่พบเรื่องร้องเรียน");
        if (complaint.WorkflowStep != ComplaintWorkflowSteps.WaitingInfo)
            throw new InvalidOperationException("เรื่องนี้ไม่ได้อยู่ระหว่างรอข้อมูลเพิ่มเติม");

        await RecordWorkflowTransitionAsync(
            complaint,
            ComplaintWorkflowSteps.Screening,
            actorId,
            "ได้รับข้อมูลเพิ่มเติมแล้ว กำลังตรวจสอบอีกครั้ง",
            ct);
    }

    public async Task CompleteClassificationAsync(int id, int actorId, CancellationToken ct = default)
    {
        var complaint = await db.Complaints.FindAsync([id], ct)
            ?? throw new InvalidOperationException("ไม่พบเรื่องร้องเรียน");
        if (complaint.WorkflowStep != ComplaintWorkflowSteps.Classification)
            throw new InvalidOperationException("ต้องผ่านขั้นตรวจสอบข้อมูลก่อน");
        if (!complaint.AssignedToId.HasValue)
            throw new InvalidOperationException("กรุณาระบุผู้รับผิดชอบ");
        if (complaint.CategoryId <= 0 || string.IsNullOrWhiteSpace(complaint.ResponsibleDepartment))
            throw new InvalidOperationException("กรุณาระบุประเภทเรื่องและหน่วยงานที่รับผิดชอบ");
        if (!complaint.SlaDeadline.HasValue)
            throw new InvalidOperationException("กรุณากำหนดวันครบกำหนด");

        await RecordWorkflowTransitionAsync(
            complaint,
            ComplaintWorkflowSteps.Investigation,
            actorId,
            "จำแนกเรื่องและวางแผนดำเนินการครบถ้วน",
            ct);
    }

    public async Task CompleteInvestigationAsync(int id, string investigationSummary,
        string? noEvidenceReason, int actorId, CancellationToken ct = default)
    {
        var complaint = await db.Complaints.FindAsync([id], ct)
            ?? throw new InvalidOperationException("ไม่พบเรื่องร้องเรียน");
        if (complaint.WorkflowStep != ComplaintWorkflowSteps.Investigation)
            throw new InvalidOperationException("ต้องอยู่ในขั้นตรวจสอบข้อเท็จจริงและดำเนินการ");
        if (string.IsNullOrWhiteSpace(investigationSummary) || investigationSummary.Trim().Length < 20)
            throw new InvalidOperationException("กรุณาสรุปผลการตรวจสอบอย่างน้อย 20 ตัวอักษร");

        var hasWorkNote = await db.ComplaintNotes.AnyAsync(
            x => x.ComplaintId == id && (x.NoteType == "InternalNote" || x.NoteType == "PublicReply"), ct);
        if (!hasWorkNote)
            throw new InvalidOperationException("กรุณาบันทึกการดำเนินการอย่างน้อยหนึ่งรายการ");

        var hasEvidence = await db.ComplaintAttachments.AnyAsync(x => x.ComplaintId == id, ct);
        if (!hasEvidence && string.IsNullOrWhiteSpace(noEvidenceReason))
            throw new InvalidOperationException("กรุณาแนบหลักฐานหรือระบุเหตุผลที่ไม่มีหลักฐาน");

        db.ComplaintNotes.Add(new ComplaintNote
        {
            ComplaintId = id,
            AuthorId = actorId,
            NoteType = "InternalNote",
            Content = hasEvidence
                ? $"สรุปผลการตรวจสอบ: {investigationSummary.Trim()}"
                : $"สรุปผลการตรวจสอบ: {investigationSummary.Trim()}\nเหตุผลที่ไม่มีหลักฐาน: {noEvidenceReason!.Trim()}",
            CreatedAt = DateTime.UtcNow
        });

        await RecordWorkflowTransitionAsync(
            complaint,
            ComplaintWorkflowSteps.Summary,
            actorId,
            "ตรวจสอบข้อเท็จจริงและบันทึกผลครบถ้วน",
            ct);
    }

    public async Task PrepareClosureAsync(int id, string resolutionNote, int actorId, CancellationToken ct = default)
    {
        var complaint = await db.Complaints.FindAsync([id], ct)
            ?? throw new InvalidOperationException("ไม่พบเรื่องร้องเรียน");
        if (complaint.WorkflowStep != ComplaintWorkflowSteps.Summary)
            throw new InvalidOperationException("ต้องผ่านขั้นตรวจสอบข้อเท็จจริงก่อน");
        if (string.IsNullOrWhiteSpace(resolutionNote) || resolutionNote.Trim().Length < 20)
            throw new InvalidOperationException("กรุณากรอกสรุปผลอย่างน้อย 20 ตัวอักษร");

        complaint.ResolutionSummary = resolutionNote.Trim();
        await RecordWorkflowTransitionAsync(
            complaint,
            ComplaintWorkflowSteps.Notification,
            actorId,
            "ตรวจ checklist และยืนยันสรุปผลก่อนแจ้งผู้ร้อง",
            ct);
    }

    public async Task ClosePreparedAsync(int id, int actorId, CancellationToken ct = default)
    {
        var complaint = await db.Complaints.FindAsync([id], ct)
            ?? throw new InvalidOperationException("ไม่พบเรื่องร้องเรียน");
        if (complaint.WorkflowStep is not (ComplaintWorkflowSteps.Notification or ComplaintWorkflowSteps.NotificationRetry))
            throw new InvalidOperationException("ต้องผ่านขั้นสรุปผลก่อน");
        if (string.IsNullOrWhiteSpace(complaint.ResolutionSummary))
            throw new InvalidOperationException("ไม่พบข้อความสรุปผลสำหรับแจ้งผู้ร้อง");

        try
        {
            await UpdateStatusAsync(id, "Closed", actorId, complaint.ResolutionSummary, ct);
        }
        catch
        {
            complaint.WorkflowStep = ComplaintWorkflowSteps.NotificationRetry;
            complaint.WorkflowUpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            throw;
        }
    }

    public async Task CloseAsync(int id, string resolutionNote, int actorId, CancellationToken ct = default)
    {
        var complaint = await db.Complaints.FindAsync([id], ct)
            ?? throw new InvalidOperationException("ไม่พบเรื่องร้องเรียน");
        if (complaint.WorkflowStep != ComplaintWorkflowSteps.Summary)
            throw new InvalidOperationException("ต้องผ่านขั้นตรวจสอบข้อเท็จจริงและอยู่ในขั้นสรุปผลก่อน");

        await PrepareClosureAsync(id, resolutionNote, actorId, ct);
        await ClosePreparedAsync(id, actorId, ct);
    }

    public async Task MoveToPreviousWorkflowStepAsync(int id, int actorId, CancellationToken ct = default)
    {
        var complaint = await db.Complaints.FindAsync([id], ct)
            ?? throw new InvalidOperationException("ไม่พบเรื่องร้องเรียน");
        if (complaint.Status is "Closed" or "Rejected")
            throw new InvalidOperationException("เรื่องที่ปิดแล้วไม่สามารถย้อนขั้นตอนได้ กรุณาเปิดเรื่องใหม่ก่อน");

        var previousStep = complaint.WorkflowStep switch
        {
            ComplaintWorkflowSteps.Classification => ComplaintWorkflowSteps.Screening,
            ComplaintWorkflowSteps.Investigation => ComplaintWorkflowSteps.Classification,
            ComplaintWorkflowSteps.Summary => ComplaintWorkflowSteps.Investigation,
            ComplaintWorkflowSteps.Notification or ComplaintWorkflowSteps.NotificationRetry => ComplaintWorkflowSteps.Summary,
            _ => throw new InvalidOperationException("ขั้นตอนปัจจุบันไม่สามารถย้อนกลับได้")
        };

        await RecordWorkflowTransitionAsync(
            complaint,
            previousStep,
            actorId,
            "เจ้าหน้าที่เลือกย้อนกลับเพื่อทบทวนหรือแก้ไขข้อมูลในขั้นก่อนหน้า",
            ct,
            "RevertComplaintWorkflow");
    }

    public async Task ReopenAsync(int id, int actorId, CancellationToken ct = default)
    {
        var complaint = await db.Complaints.FindAsync([id], ct)
            ?? throw new InvalidOperationException("Complaint not found");
        if (complaint.Status != "Closed") throw new InvalidOperationException("Only closed complaints can be reopened");

        complaint.Status = "InProgress";
        complaint.ClosedAt = null;
        complaint.UpdatedAt = DateTime.UtcNow;
        db.ComplaintNotes.Add(new ComplaintNote
        {
            ComplaintId = id, AuthorId = actorId, NoteType = "StatusChange",
            Content = "Closed→InProgress (เปิดใหม่)", CreatedAt = DateTime.UtcNow
        });
        await RecordWorkflowTransitionAsync(complaint, ComplaintWorkflowSteps.Screening, actorId, "เปิดเรื่องใหม่และกลับไปตรวจสอบความครบถ้วนอีกครั้ง", ct);
        await auditService.LogAsync("ReopenCase", actorId, null, "Complaint", id.ToString(), null, null, ct);
    }

    public async Task<IReadOnlyList<Models.Complaint>> GetQueueAsync(ComplaintQueueFilter filter, CancellationToken ct = default)
    {
        var q = ApplyFilter(db.Complaints.Include(c => c.Category).Include(c => c.AssignedTo).AsQueryable(), filter);

        return await q.OrderBy(c => c.Status == "Closed" || c.Status == "Rejected")
            .ThenBy(c => c.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
    }

    public async Task<int> GetTotalCountAsync(ComplaintQueueFilter filter, CancellationToken ct = default)
        => await ApplyFilter(db.Complaints.AsQueryable(), filter).CountAsync(ct);

    public async Task AddNoteAsync(int id, int authorId, string noteType, string content,
        IReadOnlyList<IFormFile>? attachments = null, CancellationToken ct = default)
    {
        var note = new ComplaintNote
        {
            ComplaintId = id,
            AuthorId = authorId,
            NoteType = noteType,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
        db.ComplaintNotes.Add(note);
        await db.SaveChangesAsync(ct);

        if (attachments is { Count: > 0 })
            await SaveAttachmentsAsync(id, attachments, ct, note.Id);

        await auditService.LogAsync("AddNote", authorId, null, "Complaint", id.ToString(), new { noteType }, null, ct);
    }

    public async Task SubmitReporterReplyAsync(string referenceNumber, string content,
        IReadOnlyList<IFormFile>? attachments = null, CancellationToken ct = default)
    {
        var complaint = await db.Complaints
            .FirstOrDefaultAsync(c => c.ReferenceNumber == referenceNumber, ct)
            ?? throw new InvalidOperationException("ไม่พบเรื่องร้องเรียน");
        if (complaint.WorkflowStep != ComplaintWorkflowSteps.WaitingInfo)
            throw new InvalidOperationException("เรื่องนี้ไม่ได้อยู่ระหว่างรอข้อมูลเพิ่มเติม");
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("กรุณากรอกรายละเอียดข้อมูลเพิ่มเติม");

        var note = new ComplaintNote
        {
            ComplaintId = complaint.Id,
            AuthorId = null,
            NoteType = "ReporterReply",
            Content = content.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        db.ComplaintNotes.Add(note);
        complaint.UpdatedAt = note.CreatedAt;
        await db.SaveChangesAsync(ct);

        if (attachments is { Count: > 0 })
            await SaveAttachmentsAsync(complaint.Id, attachments, ct, note.Id);

        await auditService.LogAsync(
            "ReporterSubmittedAdditionalInfo",
            null,
            null,
            "Complaint",
            complaint.Id.ToString(),
            new { complaint.ReferenceNumber, attachmentCount = attachments?.Count ?? 0 },
            null,
            ct);
    }

    public async Task<DashboardStats> GetDashboardStatsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var yesterdayStart = todayStart.AddDays(-1);
        var sevenDaysAgo = todayStart.AddDays(-6);
        var thaiCulture = new System.Globalization.CultureInfo("th-TH");
        var slaConfigs = await db.SlaConfigs.AsNoTracking().ToDictionaryAsync(x => x.Priority, ct);

        bool IsWarning(DashboardComplaintProjection complaint)
        {
            if (complaint.SlaBreached || !complaint.SlaDeadline.HasValue) return false;
            if (!slaConfigs.TryGetValue(complaint.Priority, out var config))
                return complaint.SlaDeadline <= now.AddHours(24);

            var remainingRatio = (100 - config.WarningThresholdPercent) / 100d;
            var warningWindowHours = Math.Max(1, config.ResolutionHours * remainingRatio);
            return complaint.SlaDeadline <= now.AddHours(warningWindowHours);
        }

        var todayCount = await db.Complaints.CountAsync(c => c.CreatedAt >= todayStart, ct);
        var yesterdayCount = await db.Complaints.CountAsync(c => c.CreatedAt >= yesterdayStart && c.CreatedAt < todayStart, ct);

        var activeStatuses = new[] { "Pending", "InProgress", "WaitingInfo", "Forwarded", "Resolved" };
        var activeComplaints = await db.Complaints
            .Where(c => activeStatuses.Contains(c.Status))
            .Select(c => new DashboardComplaintProjection(
                c.Id, c.ReferenceNumber, c.Category.Name, c.ReporterName, c.Priority,
                c.AssignedToId, c.SlaDeadline, c.SlaBreached, c.Status))
            .ToListAsync(ct);

        var inProgressCount = activeComplaints.Count(c => c.Status is "InProgress" or "Resolved");
        var assignedCount = activeComplaints.Count(c => c.AssignedToId.HasValue);
        var unassignedCount = activeComplaints.Count(c => !c.AssignedToId.HasValue);
        var slaBreachedCount = activeComplaints.Count(c => c.SlaBreached);
        var slaWarningCount = activeComplaints.Count(IsWarning);

        var recentDates = await db.Complaints
            .Where(c => c.CreatedAt >= sevenDaysAgo)
            .Select(c => c.CreatedAt)
            .ToListAsync(ct);

        var chartLabels = new List<string>();
        var chartData = new List<int>();
        for (var i = 6; i >= 0; i--)
        {
            var day = todayStart.AddDays(-i);
            chartLabels.Add(day.ToString("d MMM", thaiCulture));
            chartData.Add(recentDates.Count(d => d.Date == day.Date));
        }

        var warningComplaints = activeComplaints
            .Where(c => c.SlaBreached || IsWarning(c))
            .OrderBy(c => c.SlaDeadline)
            .Take(10)
            .ToList();

        var slaWarningItems = warningComplaints.Select(c =>
        {
            string remaining;
            if (c.SlaBreached || (c.SlaDeadline.HasValue && c.SlaDeadline < now))
                remaining = "เกิน SLA แล้ว";
            else if (c.SlaDeadline.HasValue)
            {
                var diff = c.SlaDeadline.Value - now;
                remaining = diff.TotalHours < 1
                    ? $"เหลือ {(int)diff.TotalMinutes} นาที"
                    : $"เหลือ {(int)diff.TotalHours} ชั่วโมง";
            }
            else
                remaining = "-";

            return new SlaWarningItem(
                c.Id,
                c.ReferenceNumber,
                c.CategoryName,
                c.ReporterName,
                c.SlaBreached || (c.SlaDeadline.HasValue && c.SlaDeadline < now),
                remaining);
        }).ToList();

        var activeStatuses2 = new[] { "Pending", "InProgress", "WaitingInfo", "Forwarded", "Resolved" };
        var categoryBreakdown = await db.Complaints
            .GroupBy(c => c.Category.Name)
            .Select(g => new
            {
                Name = g.Key,
                Total = g.Count(),
                Active = g.Count(x => activeStatuses2.Contains(x.Status))
            })
            .OrderByDescending(x => x.Total)
            .Take(5)
            .ToListAsync(ct);

        var categoryBreakdownItems = categoryBreakdown
            .Select(x => new CategoryBreakdownItem(x.Name, x.Total, x.Active))
            .ToList();

        return new DashboardStats(
            todayCount, yesterdayCount, inProgressCount, assignedCount, unassignedCount,
            slaWarningCount, slaBreachedCount, chartLabels, chartData, slaWarningItems,
            categoryBreakdownItems);
    }

    private sealed record DashboardComplaintProjection(
        int Id,
        string ReferenceNumber,
        string CategoryName,
        string ReporterName,
        string Priority,
        int? AssignedToId,
        DateTime? SlaDeadline,
        bool SlaBreached,
        string Status);

    public async Task SubmitSatisfactionAsync(string referenceNumber, byte score, string? note, CancellationToken ct = default)
    {
        var complaint = await db.Complaints.FirstOrDefaultAsync(c => c.ReferenceNumber == referenceNumber, ct)
            ?? throw new InvalidOperationException("Complaint not found");
        if (complaint.SatisfactionScore.HasValue) throw new InvalidOperationException("Already rated");
        if (score < 1 || score > 5) throw new ArgumentOutOfRangeException(nameof(score), "Score must be 1–5");

        complaint.SatisfactionScore = score;
        complaint.SatisfactionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        complaint.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Models.Complaint> ApplyFilter(IQueryable<Models.Complaint> q, ComplaintQueueFilter filter)
    {
        if (!string.IsNullOrEmpty(filter.Status)) q = q.Where(c => c.Status == filter.Status);
        if (!string.IsNullOrEmpty(filter.WorkflowStep)) q = q.Where(c => c.WorkflowStep == filter.WorkflowStep);
        if (filter.CategoryId.HasValue) q = q.Where(c => c.CategoryId == filter.CategoryId);
        if (!string.IsNullOrEmpty(filter.Priority)) q = q.Where(c => c.Priority == filter.Priority);
        if (filter.FromDate.HasValue) q = q.Where(c => c.CreatedAt >= filter.FromDate);
        if (filter.ToDate.HasValue)
        {
            var toExclusive = filter.ToDate.Value.Date.AddDays(1);
            q = q.Where(c => c.CreatedAt < toExclusive);
        }
        if (filter.AssignedToId.HasValue) q = q.Where(c => c.AssignedToId == filter.AssignedToId);
        q = filter.QuickView switch
        {
            "mine" when filter.ViewerStaffId.HasValue => q.Where(c =>
                c.AssignedToId == filter.ViewerStaffId && c.Status != "Closed" && c.Status != "Rejected"),
            "unassigned" => q.Where(c =>
                c.AssignedToId == null && c.Status != "Closed" && c.Status != "Rejected"),
            "waiting-info" => q.Where(c => c.WorkflowStep == ComplaintWorkflowSteps.WaitingInfo),
            "reporter-replied" => q.Where(c =>
                c.WorkflowStep == ComplaintWorkflowSteps.WaitingInfo &&
                c.Notes.Any(reply => reply.NoteType == "ReporterReply" &&
                    !c.Notes.Any(request => request.NoteType == "InfoRequest" && request.CreatedAt > reply.CreatedAt))),
            "sla-breached" => q.Where(c =>
                (c.SlaBreached || (c.SlaDeadline.HasValue && c.SlaDeadline < DateTime.UtcNow)) &&
                c.Status != "Closed" && c.Status != "Rejected"),
            "ready-close" => q.Where(c =>
                c.WorkflowStep == ComplaintWorkflowSteps.Summary ||
                c.WorkflowStep == ComplaintWorkflowSteps.Notification ||
                c.WorkflowStep == ComplaintWorkflowSteps.NotificationRetry),
            "closed" => q.Where(c => c.Status == "Closed"),
            _ => q
        };
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            q = q.Where(c =>
                c.ReferenceNumber.Contains(search) ||
                c.ReporterName.Contains(search) ||
                c.ReporterPhone.Contains(search) ||
                (c.ReporterEmail != null && c.ReporterEmail.Contains(search)) ||
                c.Description.Contains(search) ||
                (c.SubjectStation != null && c.SubjectStation.Contains(search)) ||
                c.Category.Name.Contains(search) ||
                (c.SubCategory != null && c.SubCategory.Name.Contains(search)) ||
                (c.ResponsibleDepartment != null && c.ResponsibleDepartment.Contains(search)));
        }
        return q;
    }

    private async Task NotifyStaffOfCriticalAsync(string refNum, CancellationToken ct)
    {
        var staffEmails = await db.StaffUsers
            .Where(u => u.IsActive && u.Role == "GeneralOfficer" && u.Email != null)
            .Select(u => u.Email!)
            .ToListAsync(ct);

        foreach (var email in staffEmails)
            await notificationService.SendAsync("CriticalReceived", null, email, new()
            {
                ["ReferenceNumber"] = refNum
            }, ct);
    }

    private async Task<string> GenerateReferenceNumberAsync(CancellationToken ct)
    {
        var thYear = DateTime.UtcNow.Year + 543;
        var seq = await NextSequenceAsync(ct);
        return $"SRT-COMPL-{thYear}-{seq:D4}";
    }

    private async Task<long> NextSequenceAsync(CancellationToken ct)
    {
        var conn = db.Database.GetDbConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT NEXT VALUE FOR dbo.ComplaintSeq";
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
    }

    private async Task RecordWorkflowTransitionAsync(
        Models.Complaint complaint,
        string nextStep,
        int actorId,
        string details,
        CancellationToken ct,
        string auditAction = "AdvanceComplaintWorkflow")
    {
        var previousStep = string.IsNullOrWhiteSpace(complaint.WorkflowStep)
            ? ComplaintWorkflowSteps.Received
            : complaint.WorkflowStep;
        var previousStatus = complaint.Status;
        var nextStatus = ComplaintStatusTransitionPolicy.StatusForWorkflow(nextStep);
        complaint.WorkflowStep = nextStep;
        complaint.Status = nextStatus;
        complaint.WorkflowUpdatedAt = DateTime.UtcNow;
        complaint.UpdatedAt = complaint.WorkflowUpdatedAt;
        db.ComplaintNotes.Add(new ComplaintNote
        {
            ComplaintId = complaint.Id,
            AuthorId = actorId,
            NoteType = "Workflow",
            Content = $"{ComplaintWorkflowSteps.ThaiLabel(previousStep)} → {ComplaintWorkflowSteps.ThaiLabel(nextStep)}: {details}",
            CreatedAt = complaint.WorkflowUpdatedAt
        });
        if (previousStatus != nextStatus)
        {
            db.ComplaintNotes.Add(new ComplaintNote
            {
                ComplaintId = complaint.Id,
                AuthorId = actorId,
                NoteType = "StatusChange",
                Content = $"{previousStatus}→{nextStatus}",
                CreatedAt = complaint.WorkflowUpdatedAt
            });
        }
        await db.SaveChangesAsync(ct);
        if (previousStatus != nextStatus)
        {
            await webhookService.QueueAsync("complaint.status_changed", new
            {
                referenceNumber = complaint.ReferenceNumber,
                externalReference = complaint.ExternalId,
                oldStatus = previousStatus,
                newStatus = nextStatus,
                workflowStep = nextStep,
                updatedAt = complaint.UpdatedAt
            }, complaint.SourcePartnerId, ct);
        }
        await auditService.LogAsync(
            auditAction,
            actorId,
            null,
            "Complaint",
            complaint.Id.ToString(),
            new { previousStep, nextStep, previousStatus, nextStatus, details },
            null,
            ct);
    }

    // Files land in {StoragePath}/{ReferenceNumber}/{guid}.ext (one folder per complaint)
    // instead of one flat folder. StoredPath carries that relative path, so older rows
    // saved before this change — which have no folder segment — keep resolving correctly
    // wherever they're read (Path.Combine(storagePath, StoredPath) works for both shapes).
    private async Task SaveAttachmentsAsync(int complaintId, IReadOnlyList<IFormFile> files, CancellationToken ct, int? noteId = null)
    {
        if (files.Count == 0) return;

        var refNumber = await db.Complaints
            .Where(c => c.Id == complaintId)
            .Select(c => c.ReferenceNumber)
            .FirstAsync(ct);

        var storagePath = config["FileUpload:StoragePath"] ?? Path.GetTempPath();
        var complaintDir = Path.Combine(storagePath, refNumber);
        Directory.CreateDirectory(complaintDir);

        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            var storedName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var fullPath = Path.Combine(complaintDir, storedName);
            await using var fs = File.Create(fullPath);
            await file.CopyToAsync(fs, ct);
            db.ComplaintAttachments.Add(new ComplaintAttachment
            {
                ComplaintId = complaintId,
                ComplaintNoteId = noteId,
                FileName = file.FileName,
                StoredPath = Path.Combine(refNumber, storedName),
                FileSize = file.Length,
                MimeType = file.ContentType,
                UploadedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
