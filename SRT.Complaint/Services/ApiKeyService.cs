using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public class ApiKeyService(AppDbContext db, IAuditService auditService, IConfiguration configuration) : IApiKeyService
{
    private readonly string? _runtimeEnvironment = ResolveRuntimeEnvironment(configuration);

    public async Task<(ApiKey key, string rawKey)> CreateAsync(CreateApiKeyRequest request, CancellationToken ct = default)
    {
        await ValidateRequestAsync(request, ct);
        ApiIntegrationPolicy.TryNormalizeIpAllowlist(request.AllowedIps, out var normalizedIps, out _);

        var environmentPrefix = request.Environment == "Sandbox" ? "test" : "live";
        var rawKey = $"srt_{environmentPrefix}_{GenerateRandomKey(32)}";
        var prefix = rawKey[..Math.Min(17, rawKey.Length)];
        var hash = HashKey(rawKey);

        var key = new ApiKey
        {
            PartnerId = request.PartnerId,
            Name = request.Name,
            KeyType = request.KeyType,
            Environment = request.Environment,
            DataAccessMode = request.DataAccessMode,
            Purpose = request.Purpose,
            KeyPrefix = prefix,
            KeyHash = hash,
            RateLimitPerMin = request.RateLimitPerMin,
            AllowedIps = normalizedIps,
            ExpiresAt = request.ExpiresAt,
            CreatedById = request.CreatedById,
            CreatedAt = DateTime.UtcNow,
            Scopes = request.Scopes.Distinct(StringComparer.Ordinal).Select(s => new ApiKeyScope { Scope = s }).ToList()
        };

        db.ApiKeys.Add(key);
        await db.SaveChangesAsync(ct);
        await auditService.LogAsync("CreateApiKey", request.CreatedById, null, "ApiKey", key.Id.ToString(), new { request.Name, request.KeyType }, null, ct);

        return (key, rawKey);
    }

    public async Task<ApiKey?> ValidateAsync(string rawKey, CancellationToken ct = default)
    {
        var hash = HashKey(rawKey);
        return await db.ApiKeys
            .Include(k => k.Partner)
            .Include(k => k.Scopes)
            .FirstOrDefaultAsync(k => k.KeyHash == hash && k.IsActive && k.Partner.IsActive &&
                                      (_runtimeEnvironment == null || k.Environment == _runtimeEnvironment) &&
                                      (k.ExpiresAt == null || k.ExpiresAt > DateTime.UtcNow), ct);
    }

    public async Task RevokeAsync(int id, int revokedById, string reason, CancellationToken ct = default)
    {
        var key = await db.ApiKeys.FindAsync([id], ct)
            ?? throw new InvalidOperationException("API key not found");
        key.IsActive = false;
        key.RevokedAt = DateTime.UtcNow;
        key.RevokedById = revokedById;
        key.RevokedReason = reason;
        await db.SaveChangesAsync(ct);
        await auditService.LogAsync("RevokeApiKey", revokedById, null, "ApiKey", id.ToString(), new { reason }, null, ct);
    }

    public async Task<(ApiKey key, string rawKey)> RotateAsync(
        int id, int rotatedById, TimeSpan gracePeriod, CancellationToken ct = default)
    {
        var oldKey = await db.ApiKeys.Include(x => x.Scopes).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("API key not found");
        if (!oldKey.IsActive || oldKey.ExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("API key นี้ไม่สามารถ Rotate ได้");

        var graceEndsAt = DateTime.UtcNow.Add(gracePeriod);
        var request = new CreateApiKeyRequest(
            oldKey.PartnerId,
            $"{oldKey.Name} (rotated {DateTime.UtcNow:yyyy-MM-dd})",
            oldKey.KeyType,
            oldKey.Environment,
            oldKey.DataAccessMode,
            oldKey.Purpose,
            oldKey.Scopes.Select(x => x.Scope).ToList(),
            oldKey.RateLimitPerMin,
            oldKey.AllowedIps,
            DateTime.UtcNow.AddDays(180),
            rotatedById);

        var result = await CreateAsync(request, ct);
        oldKey.RotatedToKeyId = result.key.Id;
        oldKey.RotationGraceEndsAt = graceEndsAt;
        if (!oldKey.ExpiresAt.HasValue || oldKey.ExpiresAt > graceEndsAt)
            oldKey.ExpiresAt = graceEndsAt;
        await db.SaveChangesAsync(ct);
        await auditService.LogAsync("RotateApiKey", rotatedById, null, "ApiKey", id.ToString(),
            new { NewKeyId = result.key.Id, GraceEndsAt = graceEndsAt }, null, ct);
        return result;
    }

    public async Task<IReadOnlyList<ApiKey>> ListAsync(CancellationToken ct = default)
        => await db.ApiKeys.Include(k => k.Partner).Include(k => k.Scopes).OrderByDescending(k => k.CreatedAt).ToListAsync(ct);

    private async Task ValidateRequestAsync(CreateApiKeyRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            throw new InvalidOperationException("ชื่อ API Key ต้องมีความยาว 1–200 ตัวอักษร");
        if (request.Purpose?.Length > 500)
            throw new InvalidOperationException("วัตถุประสงค์ต้องยาวไม่เกิน 500 ตัวอักษร");
        if (!await db.Partners.AnyAsync(x => x.Id == request.PartnerId && x.IsActive, ct))
            throw new InvalidOperationException("Partner ไม่ถูกต้องหรือถูกปิดใช้งาน");
        if (!ApiIntegrationPolicy.AllowedKeyTypes.Contains(request.KeyType))
            throw new InvalidOperationException("ประเภท API Key ไม่ถูกต้อง");
        if (!ApiIntegrationPolicy.AllowedEnvironments.Contains(request.Environment))
            throw new InvalidOperationException("Environment ไม่ถูกต้อง");
        if (_runtimeEnvironment != null && request.Environment != _runtimeEnvironment)
            throw new InvalidOperationException($"Instance นี้อนุญาตเฉพาะ API Key environment: {_runtimeEnvironment}");
        if (!ApiIntegrationPolicy.AllowedDataAccessModes.Contains(request.DataAccessMode))
            throw new InvalidOperationException("ขอบเขตข้อมูลไม่ถูกต้อง");
        if (request.Scopes.Count == 0 || request.Scopes.Any(x => !ApiIntegrationPolicy.AllowedScopes.Contains(x)))
            throw new InvalidOperationException("Scope ไม่ถูกต้อง");
        if (request.RateLimitPerMin is < 1 or > 3600)
            throw new InvalidOperationException("Rate limit ต้องอยู่ระหว่าง 1–3,600 requests/minute");
        if (!request.ExpiresAt.HasValue || request.ExpiresAt <= DateTime.UtcNow || request.ExpiresAt > DateTime.UtcNow.AddDays(366))
            throw new InvalidOperationException("วันหมดอายุต้องอยู่ภายใน 1–366 วันนับจากวันนี้");
        if (!ApiIntegrationPolicy.TryNormalizeIpAllowlist(request.AllowedIps, out _, out var error))
            throw new InvalidOperationException(error);
        if (request.DataAccessMode == "All" && request.KeyType != "Internal")
            throw new InvalidOperationException("สิทธิ์เข้าถึงข้อมูลทั้งหมดอนุญาตเฉพาะ Internal key");
        if (request.KeyType != "Internal" && request.Scopes.Contains("complaints:update", StringComparer.Ordinal))
            throw new InvalidOperationException("complaints:update อนุญาตเฉพาะ Internal key เพื่อป้องกันการข้ามขั้นตอนดำเนินงาน");
        if (request.KeyType != "Internal" && request.Scopes.Contains("corruption:stats", StringComparer.Ordinal))
            throw new InvalidOperationException("corruption:stats อนุญาตเฉพาะ Internal key");
    }

    private static string HashKey(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GenerateRandomKey(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=')[..length];
    }

    private static string? ResolveRuntimeEnvironment(IConfiguration configuration)
    {
        var value = configuration["ApiManagement:Environment"];
        return value != null && ApiIntegrationPolicy.AllowedEnvironments.Contains(value) ? value : null;
    }
}
