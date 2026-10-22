using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Polly.Timeout;
using RecordService.DataAccess.Summarization;

namespace test;

/// <summary>
/// Tests the resilience configuration Program.cs registers for the LLM client
/// (LlmHttpClientExtensions.AddLlmHttpClient): per-attempt timeout, at most one retry.
/// </summary>
public class LlmHttpClientExtensionsTests
{
    [Fact]
    public async Task HangingProvider_IsTriedTwice_EachCutOffAtTheTimeout_ThenFails()
    {
        var handler = new ScriptedHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = BuildClient(TimeSpan.FromMilliseconds(200), handler);

        var stopwatch = Stopwatch.StartNew();
        await Assert.ThrowsAsync<TimeoutRejectedException>(() => client.PostAsync("chat/completions", new StringContent("{}")));
        stopwatch.Stop();

        Assert.Equal(2, handler.CallCount); // original + one retry, never a third
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task PersistentServerError_IsTriedTwiceOnly()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var client = BuildClient(TimeSpan.FromSeconds(5), handler);

        var response = await client.PostAsync("chat/completions", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task TransientFailureThenSuccess_ReturnsTheSuccess()
    {
        var handler = new ScriptedHandler((call, _) => Task.FromResult(new HttpResponseMessage(call == 1 ? HttpStatusCode.BadGateway : HttpStatusCode.OK)));
        var client = BuildClient(TimeSpan.FromSeconds(5), handler);

        var response = await client.PostAsync("chat/completions", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task ClientError_IsNotRetried()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));
        var client = BuildClient(TimeSpan.FromSeconds(5), handler);

        await client.PostAsync("chat/completions", new StringContent("{}"));

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public void ProductionDefaults_AreThirtySecondAttempts()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), LlmHttpClientExtensions.DefaultAttemptTimeout);
    }

    private static HttpClient BuildClient(TimeSpan attemptTimeout, HttpMessageHandler primaryHandler)
    {
        var services = new ServiceCollection();
        services.AddLlmHttpClient(attemptTimeout, TimeSpan.FromMilliseconds(10))
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler);
        var client = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>().CreateClient(LlmHttpClientExtensions.HttpClientName);
        client.BaseAddress = new Uri("https://llm.test/v1/");
        return client;
    }

    private class ScriptedHandler(Func<int, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int CallCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(Interlocked.Increment(ref CallCount), cancellationToken);
    }
}
