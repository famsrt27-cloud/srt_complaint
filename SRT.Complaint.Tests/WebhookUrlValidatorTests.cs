using Microsoft.Extensions.Configuration;
using SRT.Complaint.Services;

namespace SRT.Complaint.Tests;

public class WebhookUrlValidatorTests
{
    [Theory]
    [InlineData("http://example.com/hook")]
    [InlineData("https://127.0.0.1/hook")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://192.0.2.1/hook")]
    [InlineData("https://198.18.0.1/hook")]
    [InlineData("https://[2001:db8::1]/hook")]
    public async Task UnsafeTargetsAreRejected(string url)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var validator = new WebhookUrlValidator(configuration);

        var result = await validator.ValidateAsync(url);

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }
}
