#nullable enable
using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using SRT.Complaint.Models;
using SRT.Complaint.Services;

namespace SRT.Complaint.Filters;

public class ApiKeyAuthFilter(
    IApiKeyService apiKeyService,
    IApiRequestLogService logService,
    IMemoryCache cache,
    ILogger<ApiKeyAuthFilter> logger) : IAsyncActionFilter, IOrderedFilter
{
    public int Order => -3000;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var rawKey) ||
            rawKey.Count != 1 || string.IsNullOrWhiteSpace(rawKey) || rawKey.ToString().Length > 128)
        {
            context.Result = new UnauthorizedObjectResult(ApiError(context, "authentication_required", "A valid X-API-Key header is required"));
            return;
        }

        var apiKey = await apiKeyService.ValidateAsync(rawKey!);
        if (apiKey == null)
        {
            context.Result = new UnauthorizedObjectResult(ApiError(context, "invalid_api_key", "Invalid or expired API key"));
            return;
        }

        // IP whitelist
        if (!string.IsNullOrWhiteSpace(apiKey.AllowedIps))
        {
            var clientIp = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            var allowed  = apiKey.AllowedIps
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!allowed.Any(ip => MatchesIp(clientIp, ip)))
            {
                context.Result = new ObjectResult(ApiError(context, "ip_not_allowed", "IP address not in whitelist")) { StatusCode = 403 };
                await LogRequestAsync(context, apiKey, 403, 0);
                return;
            }
        }

        // Rate limit — fixed window per minute (atomic increment)
        var cacheKey = $"ratelimit:{apiKey.Id}:{DateTime.UtcNow:yyyyMMddHHmm}";
        var bucket = cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
            return new RateLimitBucket();
        })!;
        await bucket.Gate.WaitAsync(context.HttpContext.RequestAborted);
        try
        {
            if (bucket.Count >= apiKey.RateLimitPerMin)
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                context.Result = new ObjectResult(ApiError(context, "rate_limit_exceeded", "Rate limit exceeded")) { StatusCode = 429 };
                await LogRequestAsync(context, apiKey, 429, 0);
                return;
            }
            bucket.Count++;
        }
        finally
        {
            bucket.Gate.Release();
        }

        context.HttpContext.Items["ApiKey"] = apiKey;
        context.HttpContext.Response.Headers["X-RateLimit-Limit"] = apiKey.RateLimitPerMin.ToString();

        var sw = Stopwatch.StartNew();
        var executed = await next();
        sw.Stop();

        var statusCode = ResolveStatusCode(executed.Result, executed.Exception != null ? 500 : 200);
        await LogRequestAsync(context, apiKey, statusCode, (int)sw.ElapsedMilliseconds);
    }

    private static int ResolveStatusCode(IActionResult? result, int fallback) => result switch
    {
        ObjectResult objectResult         => objectResult.StatusCode ?? fallback,
        StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
        _                                 => fallback
    };

    private async Task LogRequestAsync(ActionExecutingContext context, ApiKey apiKey, int statusCode, int durationMs)
    {
        try
        {
            var request = context.HttpContext.Request;
            await logService.LogAsync(apiKey.Id, request.Method, request.Path.Value ?? "",
                null,
                context.HttpContext.Connection.RemoteIpAddress?.ToString(), statusCode, durationMs);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to log API request");
        }
    }

    private static bool MatchesIp(string clientIp, string allowedEntry)
    {
        if (!IPAddress.TryParse(clientIp, out var clientAddr)) return false;
        if (clientAddr.IsIPv4MappedToIPv6) clientAddr = clientAddr.MapToIPv4();

        if (!allowedEntry.Contains('/'))
            return IPAddress.TryParse(allowedEntry, out var allowedAddress) && clientAddr.Equals(allowedAddress.IsIPv4MappedToIPv6 ? allowedAddress.MapToIPv4() : allowedAddress);

        var parts = allowedEntry.Split('/');
        if (parts.Length != 2) return false;
        if (!IPAddress.TryParse(parts[0], out var networkAddr)) return false;
        if (networkAddr.IsIPv4MappedToIPv6) networkAddr = networkAddr.MapToIPv4();
        if (!int.TryParse(parts[1], out var prefixLen)) return false;

        var clientBytes  = clientAddr.GetAddressBytes();
        var networkBytes = networkAddr.GetAddressBytes();
        if (clientBytes.Length != networkBytes.Length) return false;
        if (prefixLen < 0 || prefixLen > clientBytes.Length * 8) return false;

        var fullBytes = prefixLen / 8;
        var remBits   = prefixLen % 8;

        for (var i = 0; i < fullBytes; i++)
            if (clientBytes[i] != networkBytes[i]) return false;

        if (remBits > 0)
        {
            var mask = (byte)(0xFF << (8 - remBits));
            if ((clientBytes[fullBytes] & mask) != (networkBytes[fullBytes] & mask)) return false;
        }
        return true;
    }

    private static object ApiError(ActionExecutingContext context, string code, string message) => new
    {
        error = new
        {
            code,
            message,
            traceId = context.HttpContext.TraceIdentifier
        }
    };

    private sealed class RateLimitBucket
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public int Count { get; set; }
    }
}
