using StackExchange.Redis;

namespace SRT.Complaint.Services;

public sealed class RedisApiRateLimitStore(
    IConnectionMultiplexer connection,
    IConfiguration configuration,
    ILogger<RedisApiRateLimitStore> logger) : IApiRateLimitStore
{
    private const string AcquireScript = """
        local current = redis.call('INCR', KEYS[1])
        if current == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        local ttl = redis.call('PTTL', KEYS[1])
        return { current, ttl }
        """;

    private readonly string _keyPrefix = configuration["DistributedRuntime:RedisInstanceName"]
        ?? "SRT:Production:";

    public async Task<ApiRateLimitResult> AcquireAsync(
        int apiKeyId,
        int permitLimit,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var resetAt = StartOfNextMinute(now);
        var ttl = resetAt - now + TimeSpan.FromSeconds(5);
        var key = $"{_keyPrefix}ratelimit:api-key:{apiKeyId}:{now.ToUnixTimeSeconds() / 60}";

        try
        {
            var result = (RedisResult[]?)await connection.GetDatabase().ScriptEvaluateAsync(
                AcquireScript,
                [new RedisKey(key)],
                [(RedisValue)Math.Max(1, (long)Math.Ceiling(ttl.TotalMilliseconds))])
                ?? throw new RedisException("Redis rate-limit script returned no result");

            var current = (long)result[0];
            var remaining = Math.Max(0, permitLimit - (int)Math.Min(current, int.MaxValue));
            return new ApiRateLimitResult(current <= permitLimit, remaining, resetAt);
        }
        catch (Exception ex) when (ex is RedisException or TimeoutException)
        {
            logger.LogError(ex, "Distributed API rate limiter unavailable for key id {ApiKeyId}", apiKeyId);
            throw new DistributedRateLimitUnavailableException("Distributed rate limiter is unavailable", ex);
        }
    }

    private static DateTimeOffset StartOfNextMinute(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        return new DateTimeOffset(
            utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, TimeSpan.Zero).AddMinutes(1);
    }
}

public sealed class DistributedRateLimitUnavailableException(string message, Exception innerException)
    : Exception(message, innerException);
