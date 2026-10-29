using System.Net;
using System.Net.Sockets;

namespace SRT.Complaint.Services;

public static class WebhookHttpMessageHandlerFactory
{
    public static HttpMessageHandler Create(IConfiguration configuration)
    {
        var explicitlyAllowedHosts = configuration
            .GetSection("ApiManagement:AllowedWebhookHosts")
            .Get<string[]>()?
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = async (context, ct) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
                var candidates = explicitlyAllowedHosts.Contains(context.DnsEndPoint.Host)
                    ? addresses
                    : addresses.Where(x => !WebhookUrlValidator.IsPrivateOrReserved(x)).ToArray();
                if (candidates.Length == 0)
                    throw new HttpRequestException("Webhook destination resolved to a blocked network");

                Exception? lastError = null;
                foreach (var address in candidates)
                {
                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), ct);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch (Exception ex) when (ex is SocketException or OperationCanceledException)
                    {
                        lastError = ex;
                        socket.Dispose();
                        if (ex is OperationCanceledException) throw;
                    }
                }

                throw new HttpRequestException("Unable to connect to a validated webhook destination", lastError);
            }
        };
    }
}
