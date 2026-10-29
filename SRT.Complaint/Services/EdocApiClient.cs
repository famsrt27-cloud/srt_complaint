#nullable enable
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SRT.Complaint.Services;

public sealed class EdocApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<EdocOptions> options,
    ILogger<EdocApiClient> logger) : IEdocApiClient
{
    private readonly EdocOptions _options = options.Value;
    private readonly SemaphoreSlim _tokenGate = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _tokenExpiresAt;

    public bool IsConfigured => _options.Enabled;

    public async Task<EdocCreateResult> CreateDocumentAsync(EdocCreateCommand command, CancellationToken ct = default)
    {
        var payload = new
        {
            rqHeader = new { appId = _options.AppId },
            rqDetail = new
            {
                command.WID,
                Username = _options.SarabanUsername,
                BasketID = command.BasketId,
                command.RefNumber,
                From = _options.FromName,
                SendTo = command.SendTo,
                command.Subject,
                command.DocDate,
                command.Priority,
                SecretLevel = _options.SecretLevel,
                command.Description,
                command.Remark,
                _options.Wstype,
                _options.ReceiveDoc
            }
        };

        using var response = await SendAuthenticatedAsync(
            () => CreateJsonRequest(HttpMethod.Post, "api/EdocDocument/DocumentCreateOutside/", payload),
            ambiguousOutcome: true, ct);
        var body = await ReadBodyAsync(response, ct);
        EnsureSuccess(response, body, ambiguousOutcome: true);

        using var json = ParseJson(body);
        var returnedWid = FindString(json.RootElement, "WID", "wid") ?? command.WID;
        if (!string.Equals(returnedWid, command.WID, StringComparison.Ordinal))
            throw new EdocApiException("eDOC returned a different WID than requested", (int)response.StatusCode,
                ambiguousOutcome: true);

        return new EdocCreateResult(
            returnedWid,
            FindString(json.RootElement, "RegisterNO", "RegisterNo", "registerNo"),
            FindString(json.RootElement, "WDate", "wDate"));
    }

    public async Task<EdocUploadResult> UploadDocumentAsync(EdocUploadCommand command, CancellationToken ct = default)
    {
        var payload = new
        {
            rqHeader = new { appId = _options.AppId },
            rqDetail = new
            {
                command.WID,
                Username = _options.SarabanUsername,
                BasketID = command.BasketId,
                command.LinkWID,
                command.FileName,
                command.FileExtension,
                command.FileData,
                command.Detail
            }
        };

        using var response = await SendAuthenticatedAsync(
            () => CreateJsonRequest(HttpMethod.Post, "api/EdocDocument/DocumentUpload/", payload),
            ambiguousOutcome: true, ct);
        var body = await ReadBodyAsync(response, ct);
        EnsureSuccess(response, body, ambiguousOutcome: true);

        using var json = ParseJson(body);
        var statusCode = FindString(json.RootElement, "StatusCode", "statusCode", "orgStatusCode");
        var description = FindString(json.RootElement, "orgStatusDesc", "statusDescription", "message");
        if (statusCode == "-1")
            throw new EdocApiException("eDOC rejected the attachment", (int)response.StatusCode,
                FindString(json.RootElement, "orgStatusCode"));

        return new EdocUploadResult(true, statusCode, description);
    }

    public async Task<EdocInquiryResult> InquiryAsync(string wid, string basketId, CancellationToken ct = default)
    {
        var payload = new
        {
            rqHeader = new { appId = _options.AppId },
            rqDetail = new { WID = wid, Username = _options.SarabanUsername, BasketID = basketId }
        };

        using var response = await SendAuthenticatedAsync(
            () => CreateJsonRequest(HttpMethod.Get, "api/EdocDocument/DocumentInquiry/", payload),
            ambiguousOutcome: false, ct);
        var body = await ReadBodyAsync(response, ct);
        if (response.StatusCode == HttpStatusCode.BadRequest && IsNotFoundResponse(body))
            return new EdocInquiryResult(false, wid, null, null);
        EnsureSuccess(response, body, ambiguousOutcome: false);

        using var json = ParseJson(body);
        return new EdocInquiryResult(
            true,
            FindString(json.RootElement, "WID", "wid") ?? wid,
            FindString(json.RootElement, "RegisterNO", "RegisterNo", "registerNo"),
            FindString(json.RootElement, "ActionMessageDetail", "actionMessage", "message"));
    }

    public async Task<EdocTrackingResult> TrackingAsync(string wid, CancellationToken ct = default)
    {
        var path = $"api/EdocDocument/DocumentTracking/?wid={Uri.EscapeDataString(wid)}" +
                   $"&usrid={Uri.EscapeDataString(_options.SarabanUsername)}" +
                   $"&appId={Uri.EscapeDataString(_options.AppId)}";
        using var response = await SendAuthenticatedAsync(
            () => new HttpRequestMessage(HttpMethod.Get, BuildUri(path)), ambiguousOutcome: false, ct);
        var body = await ReadBodyAsync(response, ct);
        EnsureSuccess(response, body, ambiguousOutcome: false);

        using var json = ParseJson(body);
        var entries = FindTrackingEntries(json.RootElement);
        return new EdocTrackingResult(wid, entries);
    }

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        Func<HttpRequestMessage> requestFactory,
        bool ambiguousOutcome,
        CancellationToken ct)
    {
        if (!_options.Enabled) throw new InvalidOperationException("eDOC integration is disabled");

        var token = await GetTokenAsync(forceRefresh: false, ct);
        var response = await SendOnceAsync(requestFactory, token, ambiguousOutcome, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized) return response;

        response.Dispose();
        InvalidateToken(token);
        token = await GetTokenAsync(forceRefresh: true, ct);
        return await SendOnceAsync(requestFactory, token, ambiguousOutcome, ct);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        Func<HttpRequestMessage> requestFactory,
        string token,
        bool ambiguousOutcome,
        CancellationToken ct)
    {
        using var request = requestFactory();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation("access-token", token);
        try
        {
            return await httpClientFactory.CreateClient("Edoc")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new EdocApiException("Unable to determine the result of the eDOC request", ambiguousOutcome: ambiguousOutcome,
                innerException: ex);
        }
    }

    private async Task<string> GetTokenAsync(bool forceRefresh, CancellationToken ct)
    {
        if (!forceRefresh && _accessToken != null && _tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return _accessToken;

        await _tokenGate.WaitAsync(ct);
        try
        {
            if (!forceRefresh && _accessToken != null && _tokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
                return _accessToken;

            using var request = CreateJsonRequest(HttpMethod.Post, "api/Authenticate/login",
                new { username = _options.Username, password = _options.Password });
            HttpResponseMessage response;
            try
            {
                response = await httpClientFactory.CreateClient("Edoc")
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new EdocApiException("Unable to authenticate with eDOC", innerException: ex);
            }

            using (response)
            {
                var body = await ReadBodyAsync(response, ct);
                EnsureSuccess(response, body, ambiguousOutcome: false);
                using var json = ParseJson(body);
                var token = FindString(json.RootElement, "Token", "token", "access_token")
                    ?? throw new EdocApiException("eDOC login response did not contain a token", (int)response.StatusCode);
                if (token.Length > 8192)
                    throw new EdocApiException("eDOC returned an invalid token", (int)response.StatusCode);

                _accessToken = token;
                _tokenExpiresAt = ResolveTokenExpiry(json.RootElement);
                logger.LogInformation("Authenticated with eDOC; token cached until {ExpiresAt}", _tokenExpiresAt);
                return token;
            }
        }
        finally
        {
            _tokenGate.Release();
        }
    }

    private void InvalidateToken(string token)
    {
        if (string.Equals(_accessToken, token, StringComparison.Ordinal))
        {
            _accessToken = null;
            _tokenExpiresAt = default;
        }
    }

    private HttpRequestMessage CreateJsonRequest(HttpMethod method, string relativePath, object payload) => new(method, BuildUri(relativePath))
    {
        Content = JsonContent.Create(payload, options: JsonOptions)
    };

    private Uri BuildUri(string relativePath)
    {
        var normalizedBaseUrl = _options.BaseUrl.EndsWith('/') ? _options.BaseUrl : _options.BaseUrl + "/";
        return new Uri(new Uri(normalizedBaseUrl, UriKind.Absolute), relativePath);
    }

    private async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream(Math.Min(_options.MaxResponseBytes, 64 * 1024));
        var chunk = new byte[8192];
        var total = 0;
        while (true)
        {
            var read = await stream.ReadAsync(chunk, ct);
            if (read == 0) break;
            total += read;
            if (total > _options.MaxResponseBytes)
                throw new EdocApiException("eDOC response exceeded the configured size limit", (int)response.StatusCode);
            await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static JsonDocument ParseJson(string body)
    {
        try
        {
            return JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 32 });
        }
        catch (JsonException ex)
        {
            throw new EdocApiException("eDOC returned malformed JSON", innerException: ex);
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body, bool ambiguousOutcome)
    {
        if (response.IsSuccessStatusCode) return;
        var remoteCode = TryReadRemoteCode(body);
        throw new EdocApiException(
            $"eDOC returned HTTP {(int)response.StatusCode}",
            (int)response.StatusCode,
            remoteCode,
            ambiguousOutcome && (int)response.StatusCode >= 500);
    }

    private static string? TryReadRemoteCode(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            return FindString(json.RootElement, "code", "error", "StatusCode", "orgStatusCode");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsNotFoundResponse(string body) =>
        body.Contains("data not found", StringComparison.OrdinalIgnoreCase) ||
        body.Contains("error001", StringComparison.OrdinalIgnoreCase) ||
        body.Contains("error 001", StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset ResolveTokenExpiry(JsonElement root)
    {
        var value = FindString(root, "Expiration", "expiration", "expiresAt", "expires_at");
        if (DateTimeOffset.TryParse(value, out var parsed) && parsed > DateTimeOffset.UtcNow)
            return parsed;
        return DateTimeOffset.UtcNow.AddMinutes(10);
    }

    private static string? FindString(JsonElement element, params string[] names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (names.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                    return property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.Number => property.Value.GetRawText(),
                        _ => null
                    };
            }
            foreach (var property in element.EnumerateObject())
            {
                var nested = FindString(property.Value, names);
                if (nested != null) return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindString(item, names);
                if (nested != null) return nested;
            }
        }
        return null;
    }

    private static IReadOnlyList<EdocTrackingEntry> FindTrackingEntries(JsonElement root)
    {
        var candidates = new List<JsonElement>();
        CollectObjectsWithProperty(root, "statusCode", candidates);
        if (candidates.Count == 0 && root.ValueKind == JsonValueKind.Object)
            candidates.Add(root);

        return candidates.Select(item => new EdocTrackingEntry(
                FindString(item, "statusCode"),
                FindString(item, "actionMessage"),
                FindString(item, "receiveDate"),
                FindString(item, "completeDate")))
            .Where(x => x.StatusCode != null || x.ActionMessage != null)
            .ToList();
    }

    private static void CollectObjectsWithProperty(JsonElement element, string name, ICollection<JsonElement> output)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.EnumerateObject().Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
                output.Add(element);
            foreach (var property in element.EnumerateObject())
                CollectObjectsWithProperty(property.Value, name, output);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                CollectObjectsWithProperty(item, name, output);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 32
    };
}
