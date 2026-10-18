using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace test;

/// <summary>
/// Behind Caddy and the Cloudflare tunnel the TCP peer is always the proxy, so the real client address
/// must come from X-Forwarded-For, and only when the peer is the proxy (a private/Docker address).
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class ForwardedHeadersTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public ForwardedHeadersTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ForwardedFor_FromPrivateProxy_IsLoggedAsClientAddress()
    {
        var lines = await RequestLogLinesAsync(peer: "172.18.0.5", forwardedFor: "203.0.113.9");

        Assert.Contains(lines, l => l.Contains("203.0.113.9"));
    }

    [Fact]
    public async Task ForwardedFor_FromPublicPeer_IsIgnored()
    {
        var lines = await RequestLogLinesAsync(peer: "198.51.100.7", forwardedFor: "203.0.113.9");

        Assert.Contains(lines, l => l.Contains("198.51.100.7"));
        Assert.DoesNotContain(lines, l => l.Contains("203.0.113.9"));
    }

    private async Task<List<string>> RequestLogLinesAsync(string peer, string forwardedFor)
    {
        var logs = new CaptureLoggerProvider();
        using var factory = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.AddSingleton<ILoggerProvider>(logs);
            s.AddSingleton<IStartupFilter>(new SetPeerAddressFilter(IPAddress.Parse(peer)));
        }));
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/Users/me");
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        request.Headers.Add("X-Forwarded-Proto", "https");
        var response = await client.SendAsync(request);

        Assert.True((int)response.StatusCode < 500);
        return logs.Lines.ToList();
    }

    private sealed class SetPeerAddressFilter(IPAddress peer) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = peer;
                return nextMiddleware();
            });
            next(app);
        };
    }

    private sealed class CaptureLoggerProvider : ILoggerProvider, ILogger
    {
        public readonly ConcurrentQueue<string> Lines = new();
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Lines.Enqueue(formatter(state, exception));
    }
}
