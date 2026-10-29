using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using SRT.Complaint.Services;

namespace SRT.Complaint.Tests;

public class EdocApiClientTests
{
    [Fact]
    public async Task CreateDocumentAsync_AuthenticatesAutomatically_AndSendsSelectedBasket()
    {
        var requests = new List<CapturedRequest>();
        var handler = new QueueHandler(requests,
            Json(HttpStatusCode.OK, """{"Token":"token-value"}"""),
            Json(HttpStatusCode.OK, """{"rqDetail":{"WID":"SRT-0001","RegisterNO":"REG-99","WDate":"2569/10/27"}}"""));
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("Edoc")).Returns(new HttpClient(handler));
        var options = Options.Create(ValidOptions());
        var client = new EdocApiClient(factory.Object, options, Mock.Of<ILogger<EdocApiClient>>());

        var result = await client.CreateDocumentAsync(new EdocCreateCommand(
            "SRT-0001", "1234567890123", "REF-1", "ฝ่ายปลายทาง", "เรื่องทดสอบ",
            "2569/10/27", "00", "รายละเอียด", "หมายเหตุ"));

        Assert.Equal("REG-99", result.RegisterNumber);
        Assert.Equal(2, requests.Count);
        Assert.EndsWith("api/Authenticate/login", requests[0].Uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal("Bearer", requests[1].AuthorizationScheme);
        Assert.Equal("token-value", requests[1].AuthorizationParameter);
        Assert.Equal("token-value", requests[1].AccessToken);
        using var sent = JsonDocument.Parse(requests[1].Body!);
        Assert.Equal("1234567890123", sent.RootElement.GetProperty("rqDetail").GetProperty("basketID").GetString());
    }

    [Fact]
    public async Task CreateDocumentAsync_MarksServerFailureAsAmbiguous()
    {
        var handler = new QueueHandler([],
            Json(HttpStatusCode.OK, """{"Token":"token-value"}"""),
            Json(HttpStatusCode.ServiceUnavailable, """{"code":"TEMP"}"""));
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("Edoc")).Returns(new HttpClient(handler));
        var client = new EdocApiClient(factory.Object, Options.Create(ValidOptions()), Mock.Of<ILogger<EdocApiClient>>());

        var error = await Assert.ThrowsAsync<EdocApiException>(() => client.CreateDocumentAsync(new EdocCreateCommand(
            "SRT-0001", "1234567890123", "REF-1", "ฝ่ายปลายทาง", "เรื่องทดสอบ",
            "2569/10/27", "00", "รายละเอียด", "หมายเหตุ")));

        Assert.True(error.AmbiguousOutcome);
        Assert.Equal(503, error.StatusCode);
    }

    private static EdocOptions ValidOptions() => new()
    {
        Enabled = true,
        BaseUrl = "https://edoc.railway.co.th/ApiDev/RwSarabanAPI/",
        Username = "service-user",
        Password = "secret",
        SarabanUsername = "saraban-user",
        AppId = "1",
        FromName = "งานรับเรื่องร้องเรียน",
        SecretLevel = "0",
        MaxResponseBytes = 1024 * 1024
    };

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed record CapturedRequest(
        Uri Uri,
        string? Body,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string? AccessToken);

    private sealed class QueueHandler(List<CapturedRequest> requests, params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            requests.Add(new CapturedRequest(
                request.RequestUri!, body, request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.TryGetValues("access-token", out var values) ? values.Single() : null));
            return _responses.Dequeue();
        }
    }
}
