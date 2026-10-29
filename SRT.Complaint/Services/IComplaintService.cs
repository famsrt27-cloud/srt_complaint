using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public interface IComplaintService
{
    Task<Models.Complaint> SubmitAsync(SubmitComplaintRequest request, CancellationToken ct = default);
    Task<Models.Complaint?> GetByReferenceAsync(string referenceNumber, CancellationToken ct = default);
    Task<Models.Complaint?> GetByIdAsync(int id, CancellationToken ct = default);
    Task UpdateStatusAsync(int id, string newStatus, int actorId, string? note, CancellationToken ct = default);
    Task ClaimAsync(int id, int staffId, CancellationToken ct = default);
    Task TransferAsync(int id, int toOfficerId, string reason, int actorId, CancellationToken ct = default);
    Task UpdateClassificationAsync(int id, int categoryId, int? subCategoryId,
        string? responsibleDepartment, int actorId, CancellationToken ct = default);
    Task CloseAsync(int id, string resolutionNote, int actorId, CancellationToken ct = default);
    Task ReopenAsync(int id, int actorId, CancellationToken ct = default);
    Task<IReadOnlyList<Models.Complaint>> GetQueueAsync(ComplaintQueueFilter filter, CancellationToken ct = default);
    Task<int> GetTotalCountAsync(ComplaintQueueFilter filter, CancellationToken ct = default);
    Task AddNoteAsync(int id, int authorId, string noteType, string content,
        IReadOnlyList<IFormFile>? attachments = null, CancellationToken ct = default);
    Task<DashboardStats> GetDashboardStatsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WorkloadItem>> GetWorkloadAsync(CancellationToken ct = default);
    Task SubmitSatisfactionAsync(string referenceNumber, byte score, string? note, CancellationToken ct = default);
}

public record SubmitComplaintRequest(
    string ReporterName,
    string ReporterPhone,
    string? ReporterEmail,
    string? ReporterIdCard,
    int CategoryId,
    int? SubCategoryId,
    string? SubjectStation,
    DateOnly? IncidentDate,
    string Description,
    IReadOnlyList<IFormFile> Attachments,
    string Channel = "Web",
    double? Latitude = null,
    double? Longitude = null,
    int? SourcePartnerId = null,
    string? ExternalSystem = null,
    string? ExternalId = null,
    string? ReporterPrivacyChoice = null,
    string? SubmissionPurpose = null
);

public record ComplaintQueueFilter(
    string? Status = null,
    int? CategoryId = null,
    string? Priority = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int? AssignedToId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20
);

public record DashboardStats(
    int TodayCount,
    int YesterdayCount,
    int InProgressCount,
    int AssignedCount,
    int UnassignedCount,
    int SlaWarningCount,
    int SlaBreachedCount,
    List<string> ChartLabels,
    List<int> ChartData,
    List<SlaWarningItem> SlaWarningItems,
    List<CategoryBreakdownItem> CategoryBreakdown
);

public record CategoryBreakdownItem(string CategoryName, int Total, int Active);

public record SlaWarningItem(
    int Id,
    string ReferenceNumber,
    string CategoryName,
    string ReporterName,
    bool SlaBreached,
    string SlaRemainingText
);

public record WorkloadItem(
    int StaffId,
    string FullName,
    string Initials,
    int OpenCases,
    int LoadPercent
);
