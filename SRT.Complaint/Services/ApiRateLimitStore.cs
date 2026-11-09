using System.Collections.Concurrent;

namespace SRT.Complaint.Services;

public interface IApiRateLimitStore
{
    Task<ApiRateLimitResult> AcquireAsync(
        int apiKeyId,
        int permitLimit,
        DateTimeOffset now,
        CancellationToken ct = default);
}

public sealed class ApiRateLimitStore : IApiRateLimitStore
{
    private static readonly TimeSpan WindowSize = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<int, RateLimitWindow> _windows = new();

    public async Task<ApiRateLimitResult> AcquireAsync(
        int apiKeyId,
        int permitLimit,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        var window = _windows.GetOrAdd(apiKeyId, _ => new RateLimitWindow());
        await window.Gate.WaitAsync(ct);
        try
        {
            if (now >= window.ResetAt)
            {
                window.Count = 0;
                window.ResetAt = StartOfNextMinute(now);
            }

            var allowed = window.Count < permitLimit;
            if (allowed)
                window.Count++;

            return new ApiRateLimitResult(
                allowed,
                Math.Max(0, permitLimit - window.Count),
                window.ResetAt);
        }
        finally
        {
            window.Gate.Release();
        }
    }

    private static DateTimeOffset StartOfNextMinute(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        var start = new DateTimeOffset(
            utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, TimeSpan.Zero);
        return start.Add(WindowSize);
    }

    private sealed class RateLimitWindow
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public int Count { get; set; }
        public DateTimeOffset ResetAt { get; set; } = DateTimeOffset.MinValue;
    }
}

public sealed record ApiRateLimitResult(bool IsAllowed, int Remaining, DateTimeOffset ResetAt);
