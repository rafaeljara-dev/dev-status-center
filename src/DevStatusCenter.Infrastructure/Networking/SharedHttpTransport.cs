using System.Net;

namespace DevStatusCenter.Infrastructure.Networking;

public sealed class SharedHttpTransport : IDisposable
{
    private readonly SocketsHttpHandler _handler;
    private bool _disposed;

    public SharedHttpTransport()
    {
        _handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            MaxConnectionsPerServer = 4,
            UseCookies = false
        };
        Client = new HttpClient(_handler, disposeHandler: false)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("DevStatusCenter/0.1");
    }

    public HttpClient Client { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Client.Dispose();
        _handler.Dispose();
    }
}

