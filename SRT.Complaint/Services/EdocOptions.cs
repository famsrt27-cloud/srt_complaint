#nullable enable
using Microsoft.Extensions.Options;

namespace SRT.Complaint.Services;

public sealed class EdocOptions
{
    public const string SectionName = "Edoc";

    public bool Enabled { get; set; }
    public string Environment { get; set; } = "Development";
    public string BaseUrl { get; set; } = "";
    public string[] AllowedHosts { get; set; } = ["edoc.railway.co.th"];
    public bool AllowPrivateNetwork { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string AppId { get; set; } = "1";
    public string SarabanUsername { get; set; } = "";
    public string BasketId { get; set; } = "";
    public List<EdocBasketOption> Baskets { get; set; } = [];
    public Dictionary<string, string> CategoryBasketMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string FromName { get; set; } = "งานประชาสัมพันธ์และรับเรื่องราวร้องเรียน";
    public string DefaultSendTo { get; set; } = "";
    public string SecretLevel { get; set; } = "0";
    public string Wstype { get; set; } = "";
    public string ReceiveDoc { get; set; } = "";
    public bool UseBuddhistYear { get; set; } = true;
    public string DocumentDateSeparator { get; set; } = "/";
    public int MaxAttachmentBytes { get; set; } = 10 * 1024 * 1024;
    public int MaxResponseBytes { get; set; } = 1024 * 1024;
    public int TrackingIntervalMinutes { get; set; } = 30;
}

public sealed class EdocOptionsValidator : IValidateOptions<EdocOptions>
{
    public ValidateOptionsResult Validate(string? name, EdocOptions options)
    {
        if (!options.Enabled) return ValidateOptionsResult.Success;

        var errors = new List<string>();
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri) || baseUri.Scheme != Uri.UriSchemeHttps)
            errors.Add("Edoc:BaseUrl must be an absolute HTTPS URL");
        else
        {
            if (!options.AllowedHosts.Contains(baseUri.DnsSafeHost, StringComparer.OrdinalIgnoreCase))
                errors.Add("Edoc:BaseUrl host is not listed in Edoc:AllowedHosts");
            if (options.Environment.Equals("Production", StringComparison.OrdinalIgnoreCase) &&
                baseUri.AbsolutePath.Contains("ApiDev", StringComparison.OrdinalIgnoreCase))
                errors.Add("Production eDOC configuration cannot use an ApiDev URL");
        }

        if (string.IsNullOrWhiteSpace(options.Username)) errors.Add("Edoc:Username is required when enabled");
        if (string.IsNullOrWhiteSpace(options.Password)) errors.Add("Edoc:Password is required when enabled");
        if (string.IsNullOrWhiteSpace(options.SarabanUsername)) errors.Add("Edoc:SarabanUsername is required when enabled");
        if (options.AppId.Length is < 1 or > 2) errors.Add("Edoc:AppId must contain 1-2 characters");
        if (!string.IsNullOrWhiteSpace(options.BasketId) && !IsBasketId(options.BasketId))
            errors.Add("Edoc:BasketId must contain exactly 13 digits");
        if (string.IsNullOrWhiteSpace(options.BasketId) && options.Baskets.Count == 0)
            errors.Add("Configure Edoc:BasketId or at least one Edoc:Baskets item");
        if (options.Baskets.Any(x => !IsBasketId(x.Id) || string.IsNullOrWhiteSpace(x.Name)))
            errors.Add("Each Edoc:Baskets item requires a 13-digit Id and a Name");
        if (options.Baskets.GroupBy(x => x.Id, StringComparer.Ordinal).Any(x => x.Count() > 1))
            errors.Add("Edoc:Baskets cannot contain duplicate Id values");
        var allowedBaskets = options.Baskets.Select(x => x.Id).Append(options.BasketId)
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.Ordinal);
        if (options.CategoryBasketMappings.Values.Any(x => !allowedBaskets.Contains(x)))
            errors.Add("Edoc:CategoryBasketMappings contains a BasketId that is not configured");
        if (string.IsNullOrWhiteSpace(options.FromName) || options.FromName.Length > 100)
            errors.Add("Edoc:FromName is required and must be at most 100 characters");
        if (options.DefaultSendTo.Length > 100) errors.Add("Edoc:DefaultSendTo must be at most 100 characters");
        if (options.SecretLevel.Length > 1) errors.Add("Edoc:SecretLevel must be at most 1 character");
        if (options.Wstype.Length > 2) errors.Add("Edoc:Wstype must be at most 2 characters");
        if (options.ReceiveDoc.Length > 2) errors.Add("Edoc:ReceiveDoc must be at most 2 characters");
        if (options.DocumentDateSeparator is not "/" and not "-")
            errors.Add("Edoc:DocumentDateSeparator must be / or -");
        if (options.MaxAttachmentBytes is < 1 or > 20 * 1024 * 1024)
            errors.Add("Edoc:MaxAttachmentBytes must be between 1 byte and 20 MB");
        if (options.MaxResponseBytes is < 1024 or > 5 * 1024 * 1024)
            errors.Add("Edoc:MaxResponseBytes must be between 1 KB and 5 MB");
        if (options.TrackingIntervalMinutes is < 5 or > 1440)
            errors.Add("Edoc:TrackingIntervalMinutes must be between 5 and 1440");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }

    private static bool IsBasketId(string value) => value.Length == 13 && value.All(char.IsDigit);
}

public sealed class EdocBasketOption
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}
