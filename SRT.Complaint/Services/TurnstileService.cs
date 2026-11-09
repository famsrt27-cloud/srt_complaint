#nullable enable
using System.Text.Json.Serialization;

namespace SRT.Complaint.Services;

public class TurnstileService(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IWebHostEnvironment environment,
    ILogger<TurnstileService> logger) : ITurnstileService
{
    private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    public async Task<bool> VerifyAsync(string token, string? remoteIp = null)
    {
        var secretKey = config["Turnstile:SecretKey"] ?? "";

        // Local development may intentionally run without credentials. Any
        // deployed environment fails closed when the secret is missing.
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            if (environment.IsDevelopment())
            {
                logger.LogWarning("Turnstile SecretKey not configured — skipping verification in Development only");
                return true;
            }

            logger.LogError("Turnstile SecretKey is not configured; verification is unavailable");
            return false;
        }

        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var fields = new List<KeyValuePair<string, string>>
            {
                new("secret",   secretKey),
                new("response", token)
            };
            if (!string.IsNullOrWhiteSpace(remoteIp))
                fields.Add(new("remoteip", remoteIp));

            using var content  = new FormUrlEncodedContent(fields);
            using var response = await client.PostAsync(VerifyUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Turnstile verify HTTP {Status}", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>();
            if (result?.Success != true)
                logger.LogInformation("Turnstile failed — codes: {Codes}",
                    string.Join(",", result?.ErrorCodes ?? []));

            return result?.Success == true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Turnstile verification exception");
            return false;
        }
    }

    private sealed class TurnstileResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("error-codes")]
        public string[] ErrorCodes { get; init; } = [];
    }
}
