#nullable enable
using Microsoft.AspNetCore.Mvc;
using SRT.Complaint.Services;
using System.Text.Json.Serialization;

namespace SRT.Complaint.Controllers.Api;

/// <summary>
/// Receives real-time push notifications from Traffy Fondue Exchange API.
///
/// Register webhook URLs with Traffy support:
///   New issue  : POST  https://your-domain/api/traffy-webhook/new-issue
///   Status update: PATCH https://your-domain/api/traffy-webhook/update-status
///
/// Traffy signs nothing — secure by using a secret token in the path or header.
/// Set the webhook secret in Admin/ExternalSync and compare on every request.
/// </summary>
[ApiController]
[Route("api/traffy-webhook")]
public class TraffyWebhookController(
    IExternalSyncService externalSyncService,
    IExternalStatusResolver statusResolver,
    ITraffySettingsService settingsService,
    ILogger<TraffyWebhookController> logger) : ControllerBase
{
    private const string SystemKey = "traffy_fondue";

    // ─── New issue pushed by Traffy ───────────────────────────────
    [HttpPost("new-issue")]
    public async Task<IActionResult> NewIssue(
        [FromBody] TraffyWebhookIssue payload,
        CancellationToken ct)
    {
        if (!await ValidateSecretAsync(ct)) return Unauthorized();
        if (string.IsNullOrEmpty(payload.TicketId)) return BadRequest("ticket_id required");

        try
        {
            logger.LogInformation("Traffy webhook: new issue {TicketId}", payload.TicketId);

            var dto = MapToDto(payload);
            var (imported, referenceNumber) = await externalSyncService.ImportFromWebhookAsync(dto, SystemKey, ct);

            if (!imported)
            {
                logger.LogDebug("Traffy webhook: {TicketId} already imported — skip", payload.TicketId);
                return Ok(new { imported = false, reason = "duplicate" });
            }

            logger.LogInformation("Traffy webhook: imported {TicketId} → {Ref}",
                payload.TicketId, referenceNumber);

            return Ok(new { imported = true, reference = referenceNumber });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Traffy webhook: failed to import {TicketId}", payload.TicketId);
            return StatusCode(500, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Traffy webhook: unexpected error importing {TicketId}", payload.TicketId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // ─── Status update pushed by Traffy ──────────────────────────
    // NOTE: the real-time PATCH payload only carries `status` as free text (e.g. "เสร็จสิ้น") —
    // it does NOT include a status_id, unlike the polling get-issues/update-issue APIs. We
    // resolve the text against a cached get-status-list lookup (see TraffyFonduAdapter).
    [HttpPatch("update-status")]
    public async Task<IActionResult> UpdateStatus(
        [FromBody] TraffyWebhookStatusUpdate payload,
        CancellationToken ct)
    {
        if (!await ValidateSecretAsync(ct)) return Unauthorized();
        if (string.IsNullOrEmpty(payload.TicketId)) return BadRequest("ticket_id required");

        try
        {
            logger.LogInformation("Traffy webhook: status update {TicketId} → status={Status} org={Org}",
                payload.TicketId, payload.Status, payload.Org);

            string? newSrtStatus = string.IsNullOrWhiteSpace(payload.Status)
                ? null
                : await statusResolver.ResolveSrtStatusAsync(payload.Status, ct);

            var found = await externalSyncService.ApplyStatusUpdateAsync(
                SystemKey, payload.TicketId, newSrtStatus, payload.Note, ct);

            if (!found)
            {
                logger.LogDebug("Traffy webhook: {TicketId} not found in SRT system — ignore", payload.TicketId);
                return Ok(new { updated = false, reason = "not_found" });
            }

            return Ok(new { updated = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Traffy webhook: unexpected error updating status for {TicketId}", payload.TicketId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────
    private async Task<bool> ValidateSecretAsync(CancellationToken ct)
    {
        var settings = await settingsService.GetAsync(ct);
        var secret = settings.WebhookSecret;
        if (string.IsNullOrEmpty(secret)) return true; // not configured → open (dev only)

        // Accept secret via header X-Traffy-Secret or query ?secret=
        var headerVal = Request.Headers["X-Traffy-Secret"].FirstOrDefault();
        var queryVal  = Request.Query["secret"].FirstOrDefault();
        return headerVal == secret || queryVal == secret;
    }

    // The new-issue push doesn't include reporter name/phone/topic at all (see spec's
    // documented JSON POST parameters) — ReporterName/ReporterPhone always fall back to a
    // placeholder on import, same as the polling adapter.
    private static ExternalComplaintDto MapToDto(TraffyWebhookIssue i) => new(
        ExternalId:    i.TicketId,
        Description:   i.Description ?? "(ไม่มีรายละเอียด)",
        Address:       i.Address,
        CategoryHint:  i.Type,
        IncidentDate:  string.IsNullOrEmpty(i.Timestamp) ? null
                       : DateTime.TryParse(i.Timestamp, out var dt) ? dt : null,
        ReporterName:  null,
        ReporterPhone: null,
        RawStatus:     "รอรับเรื่อง",
        Latitude:      i.Latitude,
        Longitude:     i.Longitude,
        PhotoUrls:     i.Photo);

    // ─── Payload DTOs ─────────────────────────────────────────────
    public sealed class TraffyWebhookIssue
    {
        [JsonPropertyName("ticket_id")]    public string  TicketId    { get; set; } = "";
        [JsonPropertyName("description")]  public string? Description { get; set; }
        [JsonPropertyName("address")]      public string? Address     { get; set; }
        [JsonPropertyName("type")]         public string? Type        { get; set; }
        [JsonPropertyName("photo")]        public List<string>? Photo { get; set; }
        [JsonPropertyName("latitude")]     public double? Latitude    { get; set; }
        [JsonPropertyName("longitude")]    public double? Longitude   { get; set; }
        [JsonPropertyName("timestamp")]    public string? Timestamp   { get; set; }
    }

    public sealed class TraffyWebhookStatusUpdate
    {
        [JsonPropertyName("ticket_id")]  public string  TicketId { get; set; } = "";
        [JsonPropertyName("org")]        public string? Org      { get; set; }
        [JsonPropertyName("status")]     public string? Status   { get; set; }
        [JsonPropertyName("note")]       public string? Note     { get; set; }
    }
}
