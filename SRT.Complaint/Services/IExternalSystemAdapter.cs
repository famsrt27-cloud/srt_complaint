#nullable enable
namespace SRT.Complaint.Services;

public interface IExternalSystemAdapter
{
    string SystemKey    { get; }
    string DisplayName  { get; }
    string Description  { get; }
    bool   IsConfigured { get; }

    Task<ExternalFetchResult> FetchNewAsync(CancellationToken ct = default);
    Task PushStatusAsync(string externalId, string newStatus, string? note, CancellationToken ct = default);
}

public record ExternalFetchResult(
    IReadOnlyList<ExternalComplaintDto> Items,
    string? ErrorMessage = null);

public record ExternalComplaintDto(
    string   ExternalId,
    string   Description,
    string?  Address,
    string?  CategoryHint,
    DateTime? IncidentDate,
    string?  ReporterName,
    string?  ReporterPhone,
    string   RawStatus,
    double?  Latitude = null,
    double?  Longitude = null,
    IReadOnlyList<string>? PhotoUrls = null);
