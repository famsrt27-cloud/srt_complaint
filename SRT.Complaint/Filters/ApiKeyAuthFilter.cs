#nullable enable
using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;
using SRT.Complaint.Models;
using SRT.Complaint.Services;

namespace SRT.Complaint.Filters;

public class ApiKeyAuthFilter(
    IApiKeyService apiKeyService,
    IApiRequestLogService logService,
    IApiRateLimitStore rateLimitStore,
    ApiRateLimitStore emergencyRateLimitStore,
    IOptions<DistributedRuntimeOptions> distributedOptions,
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

        var apiKey = await apiKeyService.ValidateAsync(
            rawKey.ToString(), context.HttpContext.RequestAborted);
        if (apiKey == null)
        {
            context.Result = new UnauthorizedObjectResult(ApiError(context, "invalid_api_key", "Invalid or expired API key"));
            return;
        }

        if (!string.IsNullOrWhiteSpace(apiKey.AllowedIps))
        {
            var clientIp = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
            var allowed = apiKey.AllowedIps
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!allowed.Any(ip => MatchesIp(clientIp, ip)))
            {
                context.Result = new ObjectResult(ApiError(context, "ip_not_allowed", "IP address not in whitelist")) { StatusCode = 403 };
                await LogRequestAsync(context, apiKey, 403, 0);
                return;
            }
        }

        // Fixed window per API key. The selected store is local, Redis-distributed,
        // or a defense-in-depth local limiter when the gateway owns the global quota.
        var now = DateTimeOffset.UtcNow;
        var effectiveLimit = apiKey.RateLimitPerMin;
        ApiRateLimitResult rateLimit;
        try
        {
            rateLimit = await rateLimitStore.AcquireAsync(
                apiKey.Id, apiKey.RateLimitPerMin, now, context.HttpContext.RequestAborted);
        }
        catch (DistributedRateLimitUnavailableException)
        {
            if (!IsSafeReadMethod(context.HttpContext.Request.Method))
            {
                context.HttpContext.Response.Headers.RetryAfter = "5";
                context.Result = new ObjectResult(ApiError(
                    context,
                    "rate_limit_unavailable",
                    "Write requests are temporarily unavailable because quota enforcement cannot be verified"))
                {
                    StatusCode = StatusCodes.Status503ServiceUnavailable
                };
                await LogRequestAsync(context, apiKey, StatusCodes.Status503ServiceUnavailable, 0);
                return;
            }

            var emergencyLimit = Math.Clamp(
                distributedOptions.Value.EmergencyReadLimitPerMin,
                1,
                Math.Max(1, apiKey.RateLimitPerMin));
            effectiveLimit = emergencyLimit;
            rateLimit = await emergencyRateLimitStore.AcquireAsync(
                apiKey.Id, emergencyLimit, now, context.HttpContext.RequestAborted);
            logger.LogWarning(
                "Using emergency local read limit {EmergencyLimit} for API key id {ApiKeyId}",
                emergencyLimit,
                apiKey.Id);
        }

        context.HttpContext.Response.Headers["X-RateLimit-Limit"] = effectiveLimit.ToString();
        context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = rateLimit.Remaining.ToString();
        context.HttpContext.Response.Headers["X-RateLimit-Reset"] = rateLimit.ResetAt.ToUnixTimeSeconds().ToString();
        if (!rateLimit.IsAllowed)
        {
            var retryAfter = Math.Max(1, (int)Math.Ceiling((rateLimit.ResetAt - now).TotalSeconds));
            context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString();
            context.Result = new ObjectResult(ApiError(context, "rate_limit_exceeded", "Rate limit exceeded")) { StatusCode = 429 };
            await LogRequestAsync(context, apiKey, 429, 0);
            return;
        }

        context.HttpContext.Items["ApiKey"] = apiKey;

        var sw = Stopwatch.StartNew();
        var executed = await next();
        sw.Stop();

        var statusCode = ResolveStatusCode(executed.Result, executed.Exception != null ? 500 : 200);
        await LogRequestAsync(context, apiKey, statusCode, (int)sw.ElapsedMilliseconds);
    }

    internal static int ResolveStatusCode(IActionResult? result, int fallback) =>
        result is IStatusCodeActionResult statusCodeResult
            ? statusCodeResult.StatusCode ?? fallback
            : fallback;

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
            return IPAddress.TryParse(allowedEntry, out var allowedAddress) &&
                   clientAddr.Equals(allowedAddress.IsIPv4MappedToIPv6 ? allowedAddress.MapToIPv4() : allowedAddress);

        var parts = allowedEntry.Split('/');
        if (parts.Length != 2) return false;
        if (!IPAddress.TryParse(parts[0], out var networkAddr)) return false;
        if (networkAddr.IsIPv4MappedToIPv6) networkAddr = networkAddr.MapToIPv4();
        if (!int.TryParse(parts[1], out var prefixLen)) return false;

        var clientBytes = clientAddr.GetAddressBytes();
        var networkBytes = networkAddr.GetAddressBytes();
        if (clientBytes.Length != networkBytes.Length) return false;
        if (prefixLen < 0 || prefixLen > clientBytes.Length * 8) return false;

        var fullBytes = prefixLen / 8;
        var remBits = prefixLen % 8;

        for (var i = 0; i < fullBytes; i++)
            if (clientBytes[i] != networkBytes[i]) return false;

        if (remBits > 0)
        {
            var mask = (byte)(0xFF << (8 - remBits));
            if ((clientBytes[fullBytes] & mask) != (networkBytes[fullBytes] & mask)) return false;
        }
        return true;
    }

    private static bool IsSafeReadMethod(string method) =>
        HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);

    private static object ApiError(ActionExecutingContext context, string code, string message) => new
    {
        error = new
        {
            code,
            message,
            traceId = context.HttpContext.TraceIdentifier
        }
    };
}
