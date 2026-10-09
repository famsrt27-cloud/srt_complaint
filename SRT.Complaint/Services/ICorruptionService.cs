using SRT.Complaint.Models;
using SRT.Complaint.Models.Corruption;

namespace SRT.Complaint.Services;

public interface ICorruptionService
{
    Task<CorruptionReport> SubmitAsync(SubmitCorruptionRequest request, CancellationToken ct = default);
    Task<CorruptionReport?> GetByReferenceAsync(string referenceNumber, CancellationToken ct = default);
    Task<CorruptionReport?> GetByIdAsync(int id, CancellationToken ct = default);
    Task UpdateStatusAsync(int id, string newStatus, int actorId, string? note, CancellationToken ct = default);
    Task ClaimAsync(int id, int staffId, CancellationToken ct = default);
    Task CloseAsync(int id, string resolutionNote, int actorId, CancellationToken ct = default);
    Task ReopenAsync(int id, int actorId, CancellationToken ct = default);
    Task<DecryptedReporterInfo> DecryptReporterInfoAsync(int reportId, int requestedById, string reason, string ipAddress, CancellationToken ct = default);
    Task<IReadOnlyList<CorruptionReport>> GetQueueAsync(int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<CorruptionReport>> GetQueueFilteredAsync(CorruptionQueueFilter filter, CancellationToken ct = default);
    Task<int> GetTotalCountAsync(CorruptionQueueFilter filter, CancellationToken ct = default);
    Task AddInvestigationLogAsync(int reportId, int authorId, string content, bool isConfidential, CancellationToken ct = default);
    Task<CorruptionDashboardStats> GetDashboardStatsAsync(CancellationToken ct = default);
}

public record SubmitCorruptionRequest(
    string ReporterName,
    string ReporterPhone,
    string? ReporterEmail,
    string ReporterIdCard,
    string SubjectType,
    string? SubjectPersonName,
    string? SubjectDepartment,
    DateOnly? IncidentDate,
    string Description,
    IReadOnlyList<IFormFile> Attachments
);

public record DecryptedReporterInfo(
    string Name,
    string Phone,
    string? Email,
    string IdCard
);

public record CorruptionQueueFilter(
    string? Status = null,
    int Page = 1,
    int PageSize = 20
);

public record CorruptionDashboardStats(
    int TodayCount,
    int YesterdayCount,
    int PendingCount,
    int InProgressCount,
    int SlaWarningCount,
    int SlaBreachedCount,
    List<string> ChartLabels,
    List<int> ChartData,
    List<CorruptionSlaItem> SlaWarningItems
);

public record CorruptionSlaItem(
    int Id,
    string ReferenceNumber,
    string SubjectType,
    string? SubjectPersonName,
    bool SlaBreached,
    string SlaRemainingText
);
