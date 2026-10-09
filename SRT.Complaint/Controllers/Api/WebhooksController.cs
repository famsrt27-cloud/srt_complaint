#nullable enable
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SRT.Complaint.Filters;
using SRT.Complaint.Models;
using SRT.Complaint.Services;

namespace SRT.Complaint.Controllers.Api;

[ApiController]
[Route("api/webhooks")]
[ServiceFilter(typeof(ApiKeyAuthFilter))]
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

    // ─── GET /api/webhooks ────────────────────────────────────────────────────
    [HttpGet]
    [RequireScope("webhooks:manage")]
    public async Task<IActionResult> ListWebhooks(CancellationToken ct)
    {
        try
        {
            var apiKey = (HttpContext.Items["ApiKey"] as ApiKey)!;
            var webhooks = await webhookService.ListByApiKeyAsync(apiKey.Id, ct);
            return Ok(webhooks.Select(w => new
            {
                w.Id, w.Name, w.TargetUrl, w.IsActive, w.CreatedAt,
                w.LastTriggeredAt, w.LastStatusCode,
                events = ParseEvents(w.Events)
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error listing webhooks");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // ─── POST /api/webhooks ───────────────────────────────────────────────────
    [HttpPost]
    [RequireScope("webhooks:manage")]
    public async Task<IActionResult> CreateWebhook([FromBody] CreateWebhookDto dto, CancellationToken ct)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(new { error = "Invalid request", details = ModelState });

            var invalidEvents = dto.Events.Except(AllowedEvents, StringComparer.OrdinalIgnoreCase).ToList();
            if (invalidEvents.Count > 0)
                return BadRequest(new { error = $"Unknown events: {string.Join(", ", invalidEvents)}", allowedEvents = AllowedEvents });

            var apiKey = (HttpContext.Items["ApiKey"] as ApiKey)!;
            var (webhook, rawSecret) = await webhookService.CreateAsync(apiKey.Id, dto.Name, dto.TargetUrl, dto.Events, ct);

            return StatusCode(201, new
            {
                webhook.Id, webhook.Name, webhook.TargetUrl, webhook.IsActive, webhook.CreatedAt,
                events = dto.Events,
                secret = rawSecret,
                message = "เก็บ secret นี้ไว้ใช้ยืนยัน signature ของ webhook — จะไม่แสดงอีกครั้ง"
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating webhook");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // ─── DELETE /api/webhooks/{id} ────────────────────────────────────────────
    [HttpDelete("{id:int}")]
    [RequireScope("webhooks:manage")]
    public async Task<IActionResult> DeleteWebhook(int id, CancellationToken ct)
    {
        try
        {
            var apiKey = (HttpContext.Items["ApiKey"] as ApiKey)!;
            var webhooks = await webhookService.ListByApiKeyAsync(apiKey.Id, ct);
            if (!webhooks.Any(w => w.Id == id))
                return NotFound(new { error = "Webhook not found" });

            await webhookService.DeleteAsync(id, ct);
            return Ok(new { message = "ลบ webhook เรียบร้อยแล้ว" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting webhook {Id}", id);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    private static List<string> ParseEvents(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}

public record CreateWebhookDto(
    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.MaxLength(200)]
    string Name,

    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.Url]
    string TargetUrl,

    [property: System.ComponentModel.DataAnnotations.Required]
    [property: System.ComponentModel.DataAnnotations.MinLength(1)]
    List<string> Events
);
