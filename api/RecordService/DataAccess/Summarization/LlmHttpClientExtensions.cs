using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace RecordService.DataAccess.Summarization;

public static class LlmHttpClientExtensions
{
    public const string HttpClientName = "Llm";
    public static readonly TimeSpan DefaultAttemptTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Registers the named HttpClient ChatCompletionsSummaryGenerator uses: each attempt times out
    /// at <paramref name="attemptTimeout"/> and a failed attempt (timeout, 5xx, 408, 429, network
    /// error) is retried at most once, so a slow provider holds a call for at most about two
    /// timeouts. The caller already treats a failed summary as "send the Digest without one".
    /// Retry is the OUTER strategy so each attempt gets its own timeout (see
    /// PubMedHttpClientExtensions.AddPubMedHttpClient for why the order matters).
    /// </summary>
    public static IHttpClientBuilder AddLlmHttpClient(this IServiceCollection services, TimeSpan attemptTimeout, TimeSpan retryDelay)
    {
        var builder = services.AddHttpClient(HttpClientName);
        builder.AddResilienceHandler("llm", pipeline =>
        {
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 1,
                BackoffType = DelayBackoffType.Constant,
                Delay = retryDelay,
                UseJitter = false
            });
            pipeline.AddTimeout(attemptTimeout);
        });
        return builder;
    }
}
