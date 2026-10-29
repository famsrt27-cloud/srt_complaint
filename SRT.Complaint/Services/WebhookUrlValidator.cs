using System.Net;
using System.Net.Sockets;

namespace SRT.Complaint.Services;

public class WebhookUrlValidator(IConfiguration configuration) : IWebhookUrlValidator
{
    private readonly HashSet<string> _explicitlyAllowedHosts = configuration
        .GetSection("ApiManagement:AllowedWebhookHosts")
        .Get<string[]>()?
        .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

    public async Task<(bool IsValid, string? Error)> ValidateAsync(string targetUrl, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
            return (false, "Webhook URL ไม่ถูกต้อง");
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return (false, "Webhook URL ต้องใช้ HTTPS เท่านั้น");
        if (!string.IsNullOrEmpty(uri.UserInfo))
            return (false, "Webhook URL ห้ามมี username หรือ password");

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, ct);
            if (addresses.Length == 0)
                return (false, "ไม่พบ IP ของ Webhook host");

            if (!_explicitlyAllowedHosts.Contains(uri.DnsSafeHost) && addresses.Any(IsPrivateOrReserved))
                return (false, "Webhook URL ต้องไม่ชี้ไปยัง private, loopback หรือ reserved network");
        }
        catch (SocketException)
        {
            return (false, "ไม่สามารถตรวจสอบ DNS ของ Webhook host ได้");
        }

        return (true, null);
    }

    internal static bool IsPrivateOrReserved(IPAddress address)
    {
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) ||
            address.Equals(IPAddress.None) || address.Equals(IPAddress.IPv6None))
            return true;

        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 10 ||
                   bytes[0] == 127 ||
                   bytes[0] == 0 ||
                   bytes[0] >= 224 ||
                   (bytes[0] == 169 && bytes[1] == 254) ||
                   (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                   (bytes[0] == 192 && bytes[1] == 168) ||
                   (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) ||
                   (bytes[0] == 192 && bytes[1] == 0 && bytes[2] is 0 or 2) ||
                   (bytes[0] == 192 && bytes[1] == 88 && bytes[2] == 99) ||
                   (bytes[0] == 198 && bytes[1] is 18 or 19) ||
                   (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) ||
                   (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
        }

        var isDocumentationPrefix = bytes.Length == 16 && bytes[0] == 0x20 && bytes[1] == 0x01 &&
                                    bytes[2] == 0x0D && bytes[3] == 0xB8;
        return address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal ||
               (bytes[0] & 0xFE) == 0xFC || isDocumentationPrefix;
    }
}
