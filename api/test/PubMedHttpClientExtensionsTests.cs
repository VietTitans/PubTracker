using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Polly.Timeout;
using RecordService.DataAccess.ExternalSources;

namespace test;

/// <summary>
/// Tests the actual resilience configuration Program.cs registers for PubMedProvider's
/// HttpClient (PubMedHttpClientExtensions.AddPubMedHttpClient), not a hand-copied mirror of it -
/// a copy would go stale the moment someone edits the real extension method without noticing
/// this file. Added after a security/reliability review found PubMed had no bound on a hanging
/// request (the default HttpClient.Timeout is 100s) and no circuit breaker, so a slow/dead NCBI
/// could hold RecordPollingService's global poll-cycle lock for a long time, once per query,
/// every poll cycle.
/// </summary>
public class PubMedHttpClientExtensionsTests
{
    [Fact]
    public async Task SlowRequest_FailsAtTheConfiguredTimeout_NotTheHttpClientDefault()
    {
        var handler = new CountingHandler(TimeSpan.FromSeconds(5), HttpStatusCode.OK);
        var client = BuildClient(TimeSpan.FromMilliseconds(200), handler);

        var stopwatch = Stopwatch.StartNew();
        await Assert.ThrowsAsync<TimeoutRejectedException>(() => client.GetAsync("esearch.fcgi?term=x"));
        stopwatch.Stop();

        // Nowhere near the handler's 5s delay or HttpClient's 100s default - proves the
        // resilience handler's own timeout strategy is what's actually cutting this off.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2),
            $"Expected the request to fail near the configured 200ms timeout, but it took {stopwatch.ElapsedMilliseconds}ms.");
    }

    [Fact]
    public async Task RepeatedFailures_OpenTheCircuit_SoLaterCallsShortCircuitWithoutReachingTheHandler()
    {
        var handler = new CountingHandler(TimeSpan.Zero, HttpStatusCode.InternalServerError);
        var client = BuildClient(TimeSpan.FromSeconds(5), handler);

        for (var i = 0; i < 8; i++)
        {
            try
            {
                await client.GetAsync("esearch.fcgi?term=x");
            }
            catch
            {
                // Expected - every attempt fails, either by actually hitting the handler's 500
                // or, once the circuit is open, by short-circuiting instead.
            }
        }

        // If the breaker never opened, all 8 calls would have reached the handler.
        Assert.True(handler.CallCount < 8,
            $"Expected some later calls to short-circuit without reaching the handler, but it was called {handler.CallCount} times.");
    }

    // The actual motivating scenario: a hanging (not merely erroring) NCBI. This only passes if
    // the circuit breaker is the OUTER strategy and the timeout is INNER (see
    // PubMedHttpClientExtensions.AddPubMedHttpClient's doc comment) - the breaker must see each
    // attempt's TimeoutRejectedException to count it as a failure. If someone swaps that order,
    // HttpClient.Timeout's TaskCanceledException wouldn't be counted and this test would fail
    // with CallCount == 6.
    [Fact]
    public async Task RepeatedTimeouts_AlsoOpenTheCircuit_NotJustErrorResponses()
    {
        var handler = new CountingHandler(TimeSpan.FromSeconds(5), HttpStatusCode.OK);
        var client = BuildClient(TimeSpan.FromMilliseconds(200), handler);

        for (var i = 0; i < 6; i++)
        {
            try
            {
                await client.GetAsync("esearch.fcgi?term=x");
            }
            catch
            {
                // Expected - every attempt times out, either by actually waiting out the 200ms
                // timeout or, once the circuit is open, by short-circuiting instead.
            }
        }

        Assert.True(handler.CallCount < 6,
            $"Expected some later calls to short-circuit without reaching the handler, but it was called {handler.CallCount} times.");
    }

    private static HttpClient BuildClient(TimeSpan attemptTimeout, HttpMessageHandler primaryHandler)
    {
        var services = new ServiceCollection();
        services.AddPubMedHttpClient(attemptTimeout);
        services.AddHttpClient(PubMedHttpClientExtensions.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler);

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(PubMedHttpClientExtensions.HttpClientName);
        client.BaseAddress = new Uri("https://example.com/");
        return client;
    }

    private class CountingHandler(TimeSpan delay, HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int CallCount;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref CallCount);
            await Task.Delay(delay, cancellationToken);
            return new HttpResponseMessage(statusCode);
        }
    }
}
