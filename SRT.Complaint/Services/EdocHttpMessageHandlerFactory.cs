using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace SRT.Complaint.Services;

public static class EdocHttpMessageHandlerFactory
{
    public static HttpMessageHandler Create(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<EdocOptions>>().Value;
        var configuredHost = Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
            ? uri.DnsSafeHost
            : string.Empty;

        return new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectCallback = async (context, ct) =>
            {
                if (!string.Equals(context.DnsEndPoint.Host, configuredHost, StringComparison.OrdinalIgnoreCase))
                    throw new HttpRequestException("eDOC request attempted to connect to an unexpected host");

                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
                var candidates = options.AllowPrivateNetwork
                    ? addresses
                    : addresses.Where(x => !WebhookUrlValidator.IsPrivateOrReserved(x)).ToArray();
                if (candidates.Length == 0)
                    throw new HttpRequestException("eDOC host resolved only to blocked network addresses");

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

                throw new HttpRequestException("Unable to connect to the validated eDOC host", lastError);
            }
        };
    }
}
