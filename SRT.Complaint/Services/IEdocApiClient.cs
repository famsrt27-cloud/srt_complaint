#nullable enable
namespace SRT.Complaint.Services;

public interface IEdocApiClient
{
    bool IsConfigured { get; }
    Task<EdocCreateResult> CreateDocumentAsync(EdocCreateCommand command, CancellationToken ct = default);
    Task<EdocUploadResult> UploadDocumentAsync(EdocUploadCommand command, CancellationToken ct = default);
    Task<EdocInquiryResult> InquiryAsync(string wid, string basketId, CancellationToken ct = default);
    Task<EdocTrackingResult> TrackingAsync(string wid, CancellationToken ct = default);
}

public sealed record EdocCreateCommand(
    string WID,
    string BasketId,
    string RefNumber,
    string SendTo,
    string Subject,
    string DocDate,
    string Priority,
    string Description,
    string Remark);

public sealed record EdocUploadCommand(
    string WID,
    string BasketId,
    string LinkWID,
    string FileName,
    string FileExtension,
    string FileData,
    string Detail);

public sealed record EdocCreateResult(string WID, string? RegisterNumber, string? DocumentDate);
public sealed record EdocUploadResult(bool Success, string? StatusCode, string? StatusDescription);
public sealed record EdocInquiryResult(bool Found, string WID, string? RegisterNumber, string? StatusDescription);
public sealed record EdocTrackingEntry(string? StatusCode, string? ActionMessage, string? ReceiveDate, string? CompleteDate);
public sealed record EdocTrackingResult(string WID, IReadOnlyList<EdocTrackingEntry> Entries);

public sealed class EdocApiException(
    string message,
    int? statusCode = null,
    string? remoteCode = null,
    bool ambiguousOutcome = false,
    Exception? innerException = null) : Exception(message, innerException)
{
    public int? StatusCode { get; } = statusCode;
    public string? RemoteCode { get; } = remoteCode;
    public bool AmbiguousOutcome { get; } = ambiguousOutcome;
}
