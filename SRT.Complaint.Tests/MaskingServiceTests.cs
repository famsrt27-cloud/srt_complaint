using Microsoft.Extensions.Configuration;
using SRT.Complaint.Services;
using Xunit;

namespace SRT.Complaint.Tests;

public class MaskingServiceTests
{
    private static MaskingService CreateService(string? key = "lVcPxLPPSBs1SM7hSWJRLokH7BHIkZg9Q3HNegeDf+k=")
    {
        var configValues = key is null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?> { ["Encryption:Key"] = key };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        return new MaskingService(config);
    }

    [Fact]
    public void Constructor_Throws_WhenEncryptionKeyMissing()
    {
        Assert.Throws<InvalidOperationException>(() => CreateService(null));
    }

    [Fact]
    public void EncryptThenDecrypt_RoundTripsOriginalPlaintext()
    {
        var service = CreateService();
        var cipher = service.Encrypt("ทดสอบ ข้อความลับ 12345");

        var plain = service.Decrypt(cipher);

        Assert.Equal("ทดสอบ ข้อความลับ 12345", plain);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertext_ForSamePlaintext_DueToRandomIv()
    {
        var service = CreateService();

        var a = service.Encrypt("same value");
        var b = service.Encrypt("same value");

        Assert.NotEqual(Convert.ToBase64String(a), Convert.ToBase64String(b));
    }

    [Fact]
    public void MaskName_KeepsFirstCharacterOfEachPart_MasksTheRest()
    {
        var service = CreateService();

        var masked = service.MaskName("สมชาย ใจดี");

        var parts = masked.Split(' ');
        Assert.Equal(2, parts.Length);
        Assert.Equal('ส', parts[0][0]);
        Assert.All(parts[0].Skip(1), c => Assert.Equal('*', c));
        Assert.Equal('ใ', parts[1][0]);
        Assert.All(parts[1].Skip(1), c => Assert.Equal('*', c));
    }

    [Fact]
    public void MaskName_SingleCharacterPart_IsReturnedUnmasked()
    {
        var service = CreateService();

        Assert.Equal("A", service.MaskName("A"));
    }

    [Fact]
    public void MaskEmail_KeepsFirstCharacterAndDomain()
    {
        var service = CreateService();

        Assert.Equal("a****@example.com", service.MaskEmail("abcde@example.com"));
    }
}
