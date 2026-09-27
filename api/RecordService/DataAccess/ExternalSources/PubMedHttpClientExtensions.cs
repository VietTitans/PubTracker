using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace RecordService.DataAccess.ExternalSources;

public static class PubMedHttpClientExtensions
{
    public const string HttpClientName = "PubMed";

    /// <summary>
    /// Registers the named HttpClient PubMedProvider uses, with a per-request timeout and a
    /// circuit breaker - added after NCBI going down/hanging was found to have no bound: the
    /// default HttpClient.Timeout is 100s, and with RecordPollingService's global poll-cycle
    /// lock, one hanging PubMed call stalls every other user's search query behind it.
    ///
    /// The circuit breaker must be the OUTER strategy (added first) and the timeout INNER (added
    /// second): a per-request timeout firing throws TimeoutRejectedException, which the circuit
    /// breaker's default HTTP failure predicates count as a failure. HttpClient.Timeout instead
    /// throws TaskCanceledException/OperationCanceledException, which those predicates do NOT
    /// count - with the timeout applied via HttpClient.Timeout, the breaker would never open.
    ///
    /// Thresholds are sized for one poll cycle's real call volume (a handful of requests, not
    /// Polly's default MinimumThroughput of 100 in a 30s window, which this workload would never
    /// reach) - the breaker's job here is to bound how long a dead source can hold the global
    /// poll-cycle lock, not to model NCBI's true failure rate.
    /// </summary>
    public static IHttpResiliencePipelineBuilder AddPubMedHttpClient(this IServiceCollection services, TimeSpan attemptTimeout)
    {
        return services.AddHttpClient(HttpClientName)
            .AddResilienceHandler("pubmed", builder =>
            {
                builder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 1.0,
                    MinimumThroughput = 3,
                    SamplingDuration = TimeSpan.FromSeconds(Math.Max(60, attemptTimeout.TotalSeconds * 3)),
                    BreakDuration = TimeSpan.FromMinutes(10)
                });
                builder.AddTimeout(attemptTimeout);
            });
    }
}
