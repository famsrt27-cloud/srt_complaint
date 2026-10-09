#nullable enable
using System.Collections.Concurrent;
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
    ILogger<ApiKeyAuthFilter> logger) : IAsyncActionFilter
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue("X-API-Key", out var rawKey) ||
            string.IsNullOrWhiteSpace(rawKey))
        {
            context.Result = new UnauthorizedObjectResult(new { error = "X-API-Key header required" });
            return;
        }

        var apiKey = await apiKeyService.ValidateAsync(rawKey!);
        if (apiKey == null)
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Invalid or expired API key" });
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
                context.Result = new ObjectResult(new { error = "IP address not in whitelist" }) { StatusCode = 403 };
                await LogRequestAsync(context, apiKey, 403, 0);
                return;
            }
        }

        // Rate limit — fixed window per minute (atomic increment)
        var cacheKey = $"ratelimit:{apiKey.Id}:{DateTime.UtcNow:yyyyMMddHHmm}";
        var sem = _semaphores.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(context.HttpContext.RequestAborted);
        try
        {
            var count = cache.GetOrCreate(cacheKey, e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2);
                return 0;
            });
            if (count >= apiKey.RateLimitPerMin)
            {
                context.Result = new ObjectResult(new { error = "Rate limit exceeded", retryAfter = "60s" }) { StatusCode = 429 };
                await LogRequestAsync(context, apiKey, 429, 0);
                return;
            }
            cache.Set(cacheKey, count + 1, TimeSpan.FromMinutes(2));
        }
        finally
        {
            sem.Release();
        }

        context.HttpContext.Items["ApiKey"] = apiKey;

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
                request.QueryString.HasValue ? request.QueryString.Value : null,
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

        if (!allowedEntry.Contains('/'))
            return clientIp == allowedEntry;

        var parts = allowedEntry.Split('/');
        if (parts.Length != 2) return false;
        if (!IPAddress.TryParse(parts[0], out var networkAddr)) return false;
        if (!int.TryParse(parts[1], out var prefixLen)) return false;

        var clientBytes  = clientAddr.GetAddressBytes();
        var networkBytes = networkAddr.GetAddressBytes();
        if (clientBytes.Length != networkBytes.Length) return false;

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
}
