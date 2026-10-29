namespace SRT.Complaint.Services;

public interface IIdempotencyService
{
    Task<IdempotencyBeginResult> TryBeginAsync(
        int apiKeyId, string idempotencyKey, string requestHash, CancellationToken ct = default);
    Task CompleteAsync(long recordId, int responseStatus, string responseBody, CancellationToken ct = default);
    Task AbandonAsync(long recordId, CancellationToken ct = default);
}

public enum IdempotencyBeginState
{
    Started,
    Replay,
    PayloadConflict,
    InProgress
}

public record IdempotencyBeginResult(
    IdempotencyBeginState State,
    long RecordId,
    int? ResponseStatus = null,
    string? ResponseBody = null);
