using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SRT.Complaint.Services;

namespace SRT.Complaint.Tests;

public class TurnstileServiceTests
{
    [Fact]
    public async Task VerifyAsync_MissingSecretInDevelopment_AllowsLocalDevelopment()
    {
        var service = CreateService(Environments.Development, secretKey: null);

        var result = await service.VerifyAsync(string.Empty);

        Assert.True(result);
    }

    [Fact]
    public async Task VerifyAsync_MissingSecretOutsideDevelopment_FailsClosed()
    {
        var service = CreateService(Environments.Production, secretKey: null);

        var result = await service.VerifyAsync("any-token");

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyAsync_ConfiguredSecretButMissingToken_IsRejected()
    {
        var service = CreateService(Environments.Development, "test-secret");

        var result = await service.VerifyAsync(string.Empty);

        Assert.False(result);
    }

    private static TurnstileService CreateService(string environmentName, string? secretKey)
    {
        var values = new Dictionary<string, string?>();
        if (secretKey is not null)
            values["Turnstile:SecretKey"] = secretKey;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(item => item.EnvironmentName).Returns(environmentName);

        return new TurnstileService(
            Mock.Of<IHttpClientFactory>(),
            configuration,
            environment.Object,
            NullLogger<TurnstileService>.Instance);
    }
}
