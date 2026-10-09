using Microsoft.AspNetCore.Http;
using SRT.Complaint.Validation;
using Xunit;

namespace SRT.Complaint.Tests;

public class FileSignatureValidatorTests
{
    private static IFormFile MakeFile(byte[] content, string fileName = "test")
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName);
    }

    [Fact]
    public async Task HasValidSignatureAsync_ReturnsTrue_ForRealPdfHeader()
    {
        var file = MakeFile([0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34]); // "%PDF-1.4"

        Assert.True(await FileSignatureValidator.HasValidSignatureAsync(file, ".pdf"));
    }

    [Fact]
    public async Task HasValidSignatureAsync_ReturnsTrue_ForRealJpegHeader()
    {
        var file = MakeFile([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]);

        Assert.True(await FileSignatureValidator.HasValidSignatureAsync(file, ".jpg"));
    }

    [Fact]
    public async Task HasValidSignatureAsync_ReturnsFalse_WhenExecutableRenamedToPdf()
    {
        // MZ header = Windows PE executable, disguised with a .pdf extension
        var file = MakeFile([0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00]);

        Assert.False(await FileSignatureValidator.HasValidSignatureAsync(file, ".pdf"));
    }

    [Fact]
    public async Task HasValidSignatureAsync_ReturnsFalse_ForUnrecognisedExtension()
    {
        var file = MakeFile([0x25, 0x50, 0x44, 0x46]);

        Assert.False(await FileSignatureValidator.HasValidSignatureAsync(file, ".exe"));
    }

    [Fact]
    public async Task HasValidSignatureAsync_ReturnsFalse_WhenFileShorterThanSignature()
    {
        var file = MakeFile([0x25, 0x50]); // truncated %PDF

        Assert.False(await FileSignatureValidator.HasValidSignatureAsync(file, ".pdf"));
    }

    [Fact]
    public async Task HasValidSignatureAsync_ReturnsTrue_ForDocxZipHeader()
    {
        var file = MakeFile([0x50, 0x4B, 0x03, 0x04, 0x14, 0x00]);

        Assert.True(await FileSignatureValidator.HasValidSignatureAsync(file, ".docx"));
    }
}
