using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public interface IApiKeyService
{
    Task<(ApiKey key, string rawKey)> CreateAsync(CreateApiKeyRequest request, CancellationToken ct = default);
    Task<ApiKey?> ValidateAsync(string rawKey, CancellationToken ct = default);
    Task RevokeAsync(int id, int revokedById, string reason, CancellationToken ct = default);
    Task<(ApiKey key, string rawKey)> RotateAsync(int id, int rotatedById, TimeSpan gracePeriod, CancellationToken ct = default);
    Task<IReadOnlyList<ApiKey>> ListAsync(CancellationToken ct = default);
}

public record CreateApiKeyRequest(
    int PartnerId,
    string Name,
    string KeyType,
    string Environment,
    string DataAccessMode,
    string? Purpose,
    IReadOnlyList<string> Scopes,
    int RateLimitPerMin,
    string? AllowedIps,
    DateTime? ExpiresAt,
    int CreatedById
);
