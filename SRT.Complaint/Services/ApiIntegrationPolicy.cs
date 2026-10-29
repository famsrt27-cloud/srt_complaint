using System.Net;

namespace SRT.Complaint.Services;

public static class ApiIntegrationPolicy
{
    public static readonly IReadOnlySet<string> AllowedScopes = new HashSet<string>(StringComparer.Ordinal)
    {
        "complaints:read",
        "complaints:write",
        "complaints:edoc",
        "complaints:status",
        "complaints:update",
        "metadata:read",
        "stats:summary",
        "stats:detailed",
        "corruption:stats",
        "webhooks:manage"
    };

    public static readonly IReadOnlySet<string> AllowedEnvironments =
        new HashSet<string>(["Sandbox", "Production"], StringComparer.Ordinal);

    public static readonly IReadOnlySet<string> AllowedDataAccessModes =
        new HashSet<string>(["Own", "All"], StringComparer.Ordinal);

    public static readonly IReadOnlySet<string> AllowedKeyTypes =
        new HashSet<string>(["External", "Internal"], StringComparer.Ordinal);

    public static readonly IReadOnlySet<string> AllowedComplaintStatuses =
        new HashSet<string>(["Pending", "InProgress", "WaitingInfo", "Forwarded", "Resolved", "Closed", "Rejected"], StringComparer.Ordinal);

    public static bool TryNormalizeIpAllowlist(string? value, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value)) return true;

        var entries = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (entries.Length == 0) return true;
        if (entries.Length > 50)
        {
            error = "IP whitelist รองรับสูงสุด 50 รายการ";
            return false;
        }

        var result = new List<string>(entries.Length);
        foreach (var entry in entries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var parts = entry.Split('/');
            if (!IPAddress.TryParse(parts[0], out var address))
            {
                error = $"รูปแบบ IP ไม่ถูกต้อง: {entry}";
                return false;
            }

            if (parts.Length > 2)
            {
                error = $"รูปแบบ CIDR ไม่ถูกต้อง: {entry}";
                return false;
            }

            if (parts.Length == 2)
            {
                var maxPrefix = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
                if (!int.TryParse(parts[1], out var prefix) || prefix < 0 || prefix > maxPrefix)
                {
                    error = $"CIDR prefix ไม่ถูกต้อง: {entry}";
                    return false;
                }
                result.Add($"{address}/{prefix}");
            }
            else
            {
                result.Add(address.ToString());
            }
        }

        normalized = string.Join(",", result);
        return true;
    }
}
