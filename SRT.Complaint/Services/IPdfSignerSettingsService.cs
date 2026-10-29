using SRT.Complaint.Models;

namespace SRT.Complaint.Services;

public record PdfSignerProfile(
    string FullName,
    string PositionTitle,
    string ActingTitle,
    byte[]? SignatureImage,
    string? SignatureContentType);

public interface IPdfSignerSettingsService
{
    Task<PdfSignerSettings> GetAsync(CancellationToken ct = default);
    Task<PdfSignerProfile> GetProfileAsync(CancellationToken ct = default);
    Task SaveAsync(string fullName, string positionTitle, string actingTitle,
        IFormFile? signature, int updatedById, CancellationToken ct = default);
}
