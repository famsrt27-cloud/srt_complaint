namespace SRT.Complaint.Services;

public sealed class DistributedRuntimeOptions
{
    public const string SectionName = "DistributedRuntime";

    public string Mode { get; set; } = "SingleInstance";
    public string RateLimitProvider { get; set; } = "Local";
    public string RedisConnection { get; set; } = "";
    public string RedisInstanceName { get; set; } = "SRT:Production:";
    public int EmergencyReadLimitPerMin { get; set; } = 10;

    public bool IsMultiInstance => Mode.Equals("MultiInstance", StringComparison.OrdinalIgnoreCase);
    public bool UsesRedisRateLimit => RateLimitProvider.Equals("Redis", StringComparison.OrdinalIgnoreCase);
    public bool UsesGatewayRateLimit => RateLimitProvider.Equals("Gateway", StringComparison.OrdinalIgnoreCase);
}

public sealed class DataProtectionRuntimeOptions
{
    public const string SectionName = "DataProtection";

    public string ApplicationName { get; set; } = "";
    public string CertificateThumbprint { get; set; } = "";
    public string CertificateStoreLocation { get; set; } = "LocalMachine";
}
