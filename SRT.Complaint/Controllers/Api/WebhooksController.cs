#nullable enable
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SRT.Complaint.Filters;
using SRT.Complaint.Models;
using SRT.Complaint.Services;

namespace SRT.Complaint.Controllers.Api;

[ApiController]
[ApiExplorerSettings(GroupName = "v1")]
[Route("api/v1/webhooks")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
[Produces("application/json")]
public class WebhooksController(
    IWebhookService webhookService,
    ILogger<WebhooksController> logger) : ControllerBase
{
    private static readonly string[] AllowedEvents =
    [
        "complaint.created",
        "complaint.status_changed",
        "complaint.closed"
    ];

    [HttpGet(Name = "ListWebhooksV1")]
    [RequireScope("webhooks:manage")]
    public async Task<IActionResult> ListWebhooks(CancellationToken ct)
    {
        var apiKey = CurrentApiKey;
        var webhooks = await webhookService.ListByApiKeyAsync(apiKey.Id, ct);
        return Ok(webhooks.Select(w => new
        {
            w.Id,
            w.Name,
            w.TargetUrl,
            w.IsActive,
            w.CreatedAt,
            w.LastTriggeredAt,
            w.LastStatusCode,
            events = ParseEvents(w.Events)
        }));
    }

    [HttpPost(Name = "CreateWebhookV1")]
    [RequireScope("webhooks:manage")]
    [RequestSizeLimit(64 * 1024)]
    [Consumes("application/json")]
    public async Task<IActionResult> CreateWebhook([FromBody] CreateWebhookDto dto, CancellationToken ct)
    {
        try
        {
            if (dto.Events.Any(string.IsNullOrWhiteSpace))
                return BadRequest(ApiError("invalid_event", "events must not contain blank values"));

            var normalizedEvents = dto.Events
                .Select(x => x.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var invalidEvents = normalizedEvents.Except(AllowedEvents, StringComparer.Ordinal).ToList();
            if (invalidEvents.Count > 0)
                return BadRequest(ApiError("unknown_event", $"Unknown events: {string.Join(", ", invalidEvents)}"));

            var apiKey = CurrentApiKey;
            var (webhook, rawSecret) = await webhookService.CreateAsync(
                apiKey.Id, dto.Name, dto.TargetUrl, normalizedEvents, ct);

            return StatusCode(StatusCodes.Status201Created, new
            {
                webhook.Id,
                webhook.Name,
                webhook.TargetUrl,
                webhook.IsActive,
                webhook.CreatedAt,
                events = normalizedEvents,
                secret = rawSecret,
                message = "เก็บ secret นี้ไว้ตรวจสอบลายเซ็น Webhook ระบบจะไม่แสดงอีกครั้ง"
            });
        }
        catch (InvalidOperationException ex)
        {
            return UnprocessableEntity(ApiError("invalid_webhook", ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating webhook");
            return StatusCode(500, ApiError("internal_error", "Internal server error"));
        }
    }

    [HttpDelete("{id:int}", Name = "DeleteWebhookV1")]
    [RequireScope("webhooks:manage")]
    public async Task<IActionResult> DeleteWebhook(int id, CancellationToken ct)
    {
        var apiKey = CurrentApiKey;
        if (!await webhookService.DeleteForApiKeyAsync(id, apiKey.Id, ct))
            return NotFound(ApiError("not_found", "Webhook not found"));
        return NoContent();
    }

    private ApiKey CurrentApiKey => (HttpContext.Items["ApiKey"] as ApiKey)!;

    private object ApiError(string code, string message) => new
    {
        error = new { code, message, traceId = HttpContext.TraceIdentifier }
    };

    private static List<string> ParseEvents(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}

public record CreateWebhookDto(
    [param: Required, MaxLength(200)] string Name,
    [param: Required, Url, MaxLength(1000)] string TargetUrl,
    [param: Required, MinLength(1)] List<string> Events);
