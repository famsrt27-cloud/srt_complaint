namespace SRT.Complaint.Validation;

/// <summary>
/// Verifies a file's content matches its extension via magic-byte signature,
/// so a renamed executable can't slip past an extension-only allow-list.
/// </summary>
public static class FileSignatureValidator
{
    private static readonly Dictionary<string, byte[][]> SignaturesByExtension = new()
    {
        [".jpg"]  = [[0xFF, 0xD8, 0xFF]],
        [".jpeg"] = [[0xFF, 0xD8, 0xFF]],
        [".png"]  = [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],
        [".pdf"]  = [[0x25, 0x50, 0x44, 0x46]], // %PDF
        [".doc"]  = [[0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]], // legacy OLE compound file
        [".docx"] = [[0x50, 0x4B, 0x03, 0x04]], // PK.. (OOXML/ZIP container)
    };

    /// <summary>True if the extension is recognised and the file's leading bytes match one of its known signatures.</summary>
    public static async Task<bool> HasValidSignatureAsync(IFormFile file, string extension, CancellationToken ct = default)
    {
        if (!SignaturesByExtension.TryGetValue(extension, out var signatures))
            return false;

        var maxLen = signatures.Max(s => s.Length);
        var header = new byte[maxLen];

        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header.AsMemory(0, maxLen), ct);

        return signatures.Any(sig => read >= sig.Length && header.AsSpan(0, sig.Length).SequenceEqual(sig));
    }
}
