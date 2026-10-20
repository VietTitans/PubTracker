namespace RecordService.Workers;

/// <summary>
/// Pings a dead-man's-switch URL (Healthchecks.io style) after a worker cycle succeeds. The
/// monitoring service alerts when pings stop, so a stalled or crashed worker is noticed. A cycle that
/// throws does not ping. Pings closer together than <c>minInterval</c> are skipped, so a worker that
/// runs every 30 seconds does not hit the service 2,880 times a day. A failed ping is logged and
/// never breaks the worker. No URL configured means no-op.
/// </summary>
public class HeartbeatPinger
{
    private readonly HttpClient _httpClient;
    private readonly string? _url;
    private readonly TimeSpan _minInterval;
    private readonly ILogger _logger;
    private DateTime _lastPingAt = DateTime.MinValue;

    public HeartbeatPinger(HttpClient httpClient, string? url, TimeSpan minInterval, ILogger logger)
    {
        _httpClient = httpClient;
        _url = url;
        _minInterval = minInterval;
        _logger = logger;
    }

    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_url) || DateTime.UtcNow - _lastPingAt < _minInterval)
        {
            return;
        }

        // The attempt is throttled whether or not it succeeds, so a bad URL costs one call per interval.
        _lastPingAt = DateTime.UtcNow;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var response = await _httpClient.GetAsync(_url, timeout.Token);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Type only: the ping URL is a secret and must not reach the logs.
            _logger.LogWarning("Heartbeat ping failed ({Reason})", ex.GetType().Name);
        }
    }
}
