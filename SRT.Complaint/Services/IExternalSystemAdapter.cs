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

/// <summary>
/// Bridges an external system's own status vocabulary to our SRT status set.
/// Implemented by adapters whose upstream API only reports status as free text
/// (no stable status code), e.g. Traffy Fondue's real-time webhook push.
/// </summary>
public interface IExternalStatusResolver
{
    Task<string?> ResolveSrtStatusAsync(string externalStatusText, CancellationToken ct = default);
}

/// <summary>Lets the Admin settings UI invalidate a Singleton adapter's cached credentials
/// right after saving, and verify the connection on demand.</summary>
public interface ITraffyConnectionTester
{
    void InvalidateSettingsCache();
    Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken ct = default);
}
