using Microsoft.EntityFrameworkCore;
using SRT.Complaint.Data;
using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public class PdfSignerSettingsService(
    AppDbContext db,
    IConfiguration config) : IPdfSignerSettingsService
{
    private const int SettingsId = 1;

    public async Task<PdfSignerSettings> GetAsync(CancellationToken ct = default)
    {
        var settings = await db.PdfSignerSettings.FirstOrDefaultAsync(x => x.Id == SettingsId, ct);
        if (settings is not null) return settings;

        settings = new PdfSignerSettings { Id = SettingsId };
        db.PdfSignerSettings.Add(settings);
        await db.SaveChangesAsync(ct);
        return settings;
    }

    public async Task<PdfSignerProfile> GetProfileAsync(CancellationToken ct = default)
    {
        var settings = await GetAsync(ct);
        var (image, contentType) = await ReadSignatureAsync(settings.SignatureStoredPath, ct);
        return new PdfSignerProfile(
            settings.FullName,
            settings.PositionTitle,
            settings.ActingTitle,
            image,
            contentType);
    }

    public async Task SaveAsync(string fullName, string positionTitle, string actingTitle,
        IFormFile? signature, int updatedById, CancellationToken ct = default)
    {
        var settings = await GetAsync(ct);
        settings.FullName = fullName.Trim();
        settings.PositionTitle = positionTitle.Trim();
        settings.ActingTitle = actingTitle.Trim();
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedById = updatedById > 0 ? updatedById : null;

        if (signature is { Length: > 0 })
        {
            var extension = Path.GetExtension(signature.FileName).ToLowerInvariant();
            var relativePath = Path.Combine("master", "signatures", $"{Guid.NewGuid()}{extension}");
            var fullPath = ResolveStoragePath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await using var stream = File.Create(fullPath);
            await signature.CopyToAsync(stream, ct);
            settings.SignatureStoredPath = relativePath;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<(byte[]? Image, string? ContentType)> ReadSignatureAsync(
        string? storedPath, CancellationToken ct)
    {
        string? path = null;
        if (!string.IsNullOrWhiteSpace(storedPath))
        {
            path = ResolveStoragePath(storedPath);
        }
        else
        {
            var bundled = Path.Combine(AppContext.BaseDirectory, "Assets", "DefaultComplaintSignature.jpg");
            if (File.Exists(bundled)) path = bundled;
        }

        if (path is null || !File.Exists(path)) return (null, null);
        var bytes = await File.ReadAllBytesAsync(path, ct);
        var contentType = Path.GetExtension(path).ToLowerInvariant() == ".png" ? "image/png" : "image/jpeg";
        return (bytes, contentType);
    }

    private string ResolveStoragePath(string relativePath)
    {
        var root = Path.GetFullPath(config["FileUpload:StoragePath"] ?? Path.GetTempPath());
        var candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid signature storage path");
        return candidate;
    }
}
