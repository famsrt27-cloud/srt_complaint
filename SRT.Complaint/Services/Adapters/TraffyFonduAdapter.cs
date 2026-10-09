#nullable enable
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SRT.Complaint.Services.Adapters;

public class TraffyFonduAdapter : IExternalSystemAdapter, IExternalStatusResolver, ITraffyConnectionTester
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<TraffyFonduAdapter> _logger;

    // Credentials now live in the DB (Admin/ExternalSync settings form) rather than
    // appsettings.json, so they're editable at runtime without a redeploy. Cached with a
    // short TTL and invalidated immediately after a save (see InvalidateSettingsCache).
    private TraffyConnectionSettings? _cachedSettings;
    private DateTime _settingsCachedAt;
    private readonly SemaphoreSlim _settingsLock = new(1, 1);
    private static readonly TimeSpan SettingsTtl = TimeSpan.FromMinutes(5);

    // Token cache — safe because adapter is Singleton
    private string? _cachedToken;
    private long    _tokenExpiry;  // Unix timestamp (seconds)
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    // Status-list cache — status_id values are per-org custom IDs (see get-status-list),
    // not universal constants, so we look them up instead of hardcoding.
    private List<TraffyStatus>? _cachedStatusList;
    private DateTime _statusListCachedAt;
    private readonly SemaphoreSlim _statusListLock = new(1, 1);
    private static readonly TimeSpan StatusListTtl = TimeSpan.FromHours(6);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public TraffyFonduAdapter(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpFactory,
        ILogger<TraffyFonduAdapter> logger)
    {
        _scopeFactory = scopeFactory;
        _httpFactory  = httpFactory;
        _logger       = logger;
    }

    public string SystemKey   => "traffy_fondue";
    public string DisplayName => "Traffy Fondue";
    public string Description => "ระบบรับแจ้งปัญหาของ NECTEC/NSTDA";

    // IExternalSystemAdapter.IsConfigured is synchronous (checked from Razor views), but
    // settings now live in the DB — block briefly on the cached/short-TTL fetch rather than
    // widening the interface across every adapter. ASP.NET Core has no captured
    // SynchronizationContext, so this doesn't risk a deadlock; a transient failure just
    // reports "not configured" instead of throwing during page render.
    public bool IsConfigured
    {
        get
        {
            try
            {
                return GetSettingsAsync(CancellationToken.None).GetAwaiter().GetResult().IsConfigured;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TraffyFondue: failed to load settings while checking IsConfigured");
                return false;
            }
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Fetch
    // ────────────────────────────────────────────────────────────
    public async Task<ExternalFetchResult> FetchNewAsync(CancellationToken ct = default)
    {
        try
        {
            var settings = await GetSettingsAsync(ct);
            var token    = await GetTokenAsync(ct);
            var client   = CreateClient(token);

            // "duration" only supports "today" or "all" per the Traffy Exchange API spec —
            // there is no "week"/arbitrary range. "today" must be polled at least once a
            // day (handled by ExternalSyncBackgroundService) or issues from missed days are
            // unrecoverable.
            var url      = $"{settings.ApiUrl.TrimEnd('/')}/get-issues/v1?org_id={settings.OrgId}&duration=today";
            var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json   = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<TraffyIssuesResponse>(json, JsonOpts)
                         ?? throw new InvalidOperationException("Response null");
            EnsureTraffySuccess(result.Status, result.Message, "get-issues");

            var items = result.Results.Select(MapToDto).ToList();
            _logger.LogInformation("TraffyFondue fetched {Count} issues", items.Count);
            return new ExternalFetchResult(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TraffyFondue FetchNewAsync failed");
            return new ExternalFetchResult([], ex.Message);
        }
    }

    // ────────────────────────────────────────────────────────────
    //  Push status back to Traffy
    // ────────────────────────────────────────────────────────────
    public async Task PushStatusAsync(string externalId, string newStatus, string? note, CancellationToken ct = default)
    {
        var settings   = await GetSettingsAsync(ct);
        var token      = await GetTokenAsync(ct);
        var client     = CreateClient(token);
        var statusList = await GetStatusListAsync(ct);

        var targetType = SrtStatusToTraffyStatusType(newStatus);
        var match = statusList.FirstOrDefault(s => s.StatusType == targetType)
            ?? throw new InvalidOperationException(
                $"ไม่พบสถานะ Traffy ที่ตรงกับ status_type '{targetType}' สำหรับ org_id {settings.OrgId} — ตรวจสอบ get-status-list");

        var body = JsonSerializer.Serialize(new
        {
            ticket_id = externalId,
            status_id = match.StatusId,
            note      = note ?? ""
        });

        var content  = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await client.PatchAsync($"{settings.ApiUrl.TrimEnd('/')}/update-issue/v1", content, ct);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var result = JsonSerializer.Deserialize<TraffyStatusUpdateResponse>(responseJson, JsonOpts);
        if (result != null)
            EnsureTraffySuccess(result.Status, result.Message, "update-issue");
    }

    // ────────────────────────────────────────────────────────────
    //  Resolve a Traffy status *text* (as sent by the real-time webhook,
    //  which never includes status_id) back to our SRT status
    // ────────────────────────────────────────────────────────────
    public async Task<string?> ResolveSrtStatusAsync(string externalStatusText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(externalStatusText)) return null;

        var statusList = await GetStatusListAsync(ct);
        var match = statusList.FirstOrDefault(s =>
            string.Equals(s.Status, externalStatusText, StringComparison.Ordinal));

        if (match == null)
        {
            _logger.LogWarning("TraffyFondue: unrecognised status text '{Status}' — not in cached get-status-list", externalStatusText);
            return null;
        }

        return TraffyStatusTypeToSrtStatus(match.StatusType);
    }

    // ────────────────────────────────────────────────────────────
    //  Admin settings management (Admin/ExternalSync)
    // ────────────────────────────────────────────────────────────
    public void InvalidateSettingsCache()
    {
        _cachedSettings = null;
        _cachedToken = null;
        _tokenExpiry = 0;
        _cachedStatusList = null;
    }

    public async Task<(bool Success, string Message)> TestConnectionAsync(CancellationToken ct = default)
    {
        InvalidateSettingsCache();
        try
        {
            var settings = await GetSettingsAsync(ct);
            if (!settings.IsConfigured)
                return (false, "ยังไม่ได้ตั้งค่า Username/OrgId");

            await GetTokenAsync(ct);
            return (true, "เชื่อมต่อสำเร็จ — ได้รับ token จาก Traffy Fondue เรียบร้อยแล้ว");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<TraffyConnectionSettings> GetSettingsAsync(CancellationToken ct)
    {
        if (_cachedSettings != null && DateTime.UtcNow - _settingsCachedAt < SettingsTtl)
            return _cachedSettings;

        await _settingsLock.WaitAsync(ct);
        try
        {
            if (_cachedSettings != null && DateTime.UtcNow - _settingsCachedAt < SettingsTtl)
                return _cachedSettings;

            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ITraffySettingsService>();
            var settings = await settingsService.GetAsync(ct);

            _cachedSettings   = settings;
            _settingsCachedAt = DateTime.UtcNow;
            return settings;
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    private async Task<List<TraffyStatus>> GetStatusListAsync(CancellationToken ct)
    {
        if (_cachedStatusList != null && DateTime.UtcNow - _statusListCachedAt < StatusListTtl)
            return _cachedStatusList;

        await _statusListLock.WaitAsync(ct);
        try
        {
            if (_cachedStatusList != null && DateTime.UtcNow - _statusListCachedAt < StatusListTtl)
                return _cachedStatusList;

            var settings = await GetSettingsAsync(ct);
            var token    = await GetTokenAsync(ct);
            var client   = CreateClient(token);
            var url      = $"{settings.ApiUrl.TrimEnd('/')}/get-status-list/v1?org_id={settings.OrgId}";
            var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json   = await response.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<TraffyStatusListResponse>(json, JsonOpts)
                         ?? throw new InvalidOperationException("get-status-list response null");
            EnsureTraffySuccess(result.Status, result.Message, "get-status-list");

            _cachedStatusList  = result.Results;
            _statusListCachedAt = DateTime.UtcNow;
            return _cachedStatusList;
        }
        finally
        {
            _statusListLock.Release();
        }
    }

    // Our SRT statuses <-> Traffy's status_type vocabulary (start/inprogress/finish/irrelevant/follow).
    // status_type is the one part of Traffy's status model that IS stable across orgs — status_id and
    // the display text are both org-specific and must be looked up via get-status-list.
    private static string SrtStatusToTraffyStatusType(string srtStatus) => srtStatus switch
    {
        "Pending"                                                    => "start",
        "InProgress" or "WaitingInfo" or "Forwarded" or "UnderReview" => "inprogress",
        "Resolved" or "Closed"                                       => "finish",
        "Rejected"                                                   => "irrelevant",
        _                                                             => "inprogress"
    };

    private static string TraffyStatusTypeToSrtStatus(string statusType) => statusType switch
    {
        "start"      => "Pending",
        "inprogress" => "InProgress",
        "finish"     => "Resolved",
        "irrelevant" => "Rejected",
        "follow"     => "InProgress",
        _            => "InProgress"
    };

    // ────────────────────────────────────────────────────────────
    //  JWT token management
    // ────────────────────────────────────────────────────────────
    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (_cachedToken != null && _tokenExpiry > now + 60)
            return _cachedToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (_cachedToken != null && _tokenExpiry > now + 60)
                return _cachedToken;

            var settings = await GetSettingsAsync(ct);
            var client   = _httpFactory.CreateClient();
            var payload  = JsonSerializer.Serialize(new { user = settings.Username, pass = settings.Password });
            var content  = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{settings.ApiUrl.TrimEnd('/')}/get-auth/v1", content, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var (token, expireUnix, status, message) = ParseAuthResponse(json);
            EnsureTraffySuccess(status, message, "get-auth");

            _cachedToken = token;
            _tokenExpiry = expireUnix;
            _logger.LogInformation("TraffyFondue token refreshed, expires {Expiry}", _tokenExpiry);
            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    /// <summary>
    /// The Exchange API doc's own example for get-auth's response is inconsistent (its "results"
    /// example mixes array and object JSON syntax), and shows expire_timestamp as a formatted
    /// date string despite most other numeric fields being plain numbers. This parses defensively:
    /// accepts "results" as either an object or a one-element array, and accepts expire_timestamp
    /// as either a Unix-seconds number or a "yyyy-MM-dd HH:mm:ss" string (documented as UTC+7 when
    /// no timezone is given). Verify against a live account and simplify once confirmed.
    /// </summary>
    private (string Token, long ExpireUnixSeconds, string? Status, string? Message) ParseAuthResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var status  = root.TryGetProperty("status", out var s) ? s.GetString() : null;
        var message = root.TryGetProperty("message", out var m) ? m.GetString() : null;

        JsonElement resultElement;
        if (root.TryGetProperty("results", out var results))
        {
            resultElement = results.ValueKind switch
            {
                JsonValueKind.Array when results.GetArrayLength() > 0 => results[0],
                JsonValueKind.Object                                  => results,
                _ => throw new InvalidOperationException(
                    $"TraffyFondue get-auth: unexpected 'results' shape ({results.ValueKind}). Raw: {json}")
            };
        }
        else if (root.TryGetProperty("token", out _))
        {
            _logger.LogWarning("TraffyFondue get-auth: 'token' found at top level, not under 'results' — unexpected per documented shape");
            resultElement = root;
        }
        else
        {
            throw new InvalidOperationException($"TraffyFondue get-auth: could not locate token in response. Raw: {json}");
        }

        var token = resultElement.TryGetProperty("token", out var t) ? t.GetString() : null;
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException($"TraffyFondue get-auth: token missing/empty. Raw: {json}");

        if (!resultElement.TryGetProperty("expire_timestamp", out var expireRaw))
            throw new InvalidOperationException($"TraffyFondue get-auth: expire_timestamp missing. Raw: {json}");

        long expireUnix = expireRaw.ValueKind switch
        {
            JsonValueKind.Number => expireRaw.GetInt64(),
            JsonValueKind.String => ParseUtc7TimestampToUnixSeconds(expireRaw.GetString()!),
            _ => throw new InvalidOperationException(
                $"TraffyFondue get-auth: unexpected expire_timestamp type ({expireRaw.ValueKind}). Raw: {json}")
        };

        return (token, expireUnix, status, message);
    }

    /// <summary>Traffy timestamps with no explicit timezone are documented as UTC+7.</summary>
    private static long ParseUtc7TimestampToUnixSeconds(string ts)
    {
        var local = DateTime.Parse(ts, CultureInfo.InvariantCulture, DateTimeStyles.None);
        var utc7  = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(7));
        return utc7.ToUnixTimeSeconds();
    }

    /// <summary>Traffy reports application-level errors as {"status":"fail"/"warning", "message": "..."} inside a 200 OK — HTTP status alone doesn't surface these.</summary>
    private void EnsureTraffySuccess(string? status, string? message, string apiName)
    {
        if (string.Equals(status, "fail", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Traffy {apiName} error: {message}");

        if (string.Equals(status, "warning", StringComparison.OrdinalIgnoreCase))
            _logger.LogWarning("TraffyFondue {Api} warning: {Message}", apiName, message);
    }

    // ────────────────────────────────────────────────────────────
    //  Mapping helpers
    // ────────────────────────────────────────────────────────────
    private HttpClient CreateClient(string token)
    {
        var client = _httpFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // get-issues doesn't return reporter name/phone/topic at all (only available via the
    // single-ticket get-issue's timeline, which the API doc says must not be exposed publicly) —
    // so ReporterName/ReporterPhone are always null here and fall back to a placeholder on import.
    private static ExternalComplaintDto MapToDto(TraffyIssue i) => new(
        ExternalId:    i.TicketId,
        Description:   i.Description ?? "(ไม่มีรายละเอียด)",
        Address:       i.Address,
        CategoryHint:  i.Type,
        IncidentDate:  ParseTimestamp(i.Timestamp),
        ReporterName:  null,
        ReporterPhone: null,
        RawStatus:     i.Status ?? "รอรับเรื่อง",
        Latitude:      i.Latitude,
        Longitude:     i.Longitude,
        PhotoUrls:     string.IsNullOrEmpty(i.Photo) ? null : [i.Photo]);

    private static DateTime? ParseTimestamp(string? ts)
        => string.IsNullOrEmpty(ts) ? null
           : DateTime.TryParse(ts, out var dt) ? dt : null;

    // ────────────────────────────────────────────────────────────
    //  Internal DTOs
    // ────────────────────────────────────────────────────────────
    private sealed class TraffyIssuesResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("results")]
        public List<TraffyIssue> Results { get; set; } = [];
    }

    private sealed class TraffyIssue
    {
        [JsonPropertyName("ticket_id")]
        public string TicketId { get; set; } = "";

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("photo")]
        public string? Photo { get; set; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    private sealed class TraffyStatusListResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("results")]
        public List<TraffyStatus> Results { get; set; } = [];
    }

    private sealed class TraffyStatus
    {
        [JsonPropertyName("status_id")]
        public int StatusId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "";

        [JsonPropertyName("status_type")]
        public string StatusType { get; set; } = "";
    }

    private sealed class TraffyStatusUpdateResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
