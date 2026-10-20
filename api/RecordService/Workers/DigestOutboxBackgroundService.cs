using RecordService.BusinessLogic.DigestOutboxService;

namespace RecordService.Workers;

/// <summary>
/// Drains the digest outbox on an interval: sends queued digest emails with retry. Runs a pass
/// immediately at startup so rows left by a crash are picked up.
/// </summary>
public class DigestOutboxBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DigestOutboxBackgroundService> _logger;
    private readonly TimeSpan _interval;
    private readonly HeartbeatPinger? _heartbeat;

    public DigestOutboxBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DigestOutboxBackgroundService> logger,
        TimeSpan interval,
        HeartbeatPinger? heartbeat = null)
    {
        _heartbeat = heartbeat;
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _interval = interval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var outbox = scope.ServiceProvider.GetRequiredService<IDigestOutboxService>();
                var sent = await outbox.ProcessDueAsync(stoppingToken);
                if (sent > 0)
                {
                    _logger.LogInformation("Digest outbox: sent {Count} digest(s)", sent);
                }
                // A pass whose sends failed (Brevo down, key revoked) does not ping: that silence is the alert.
                if (_heartbeat != null && outbox.LastPassSendFailures == 0)
                {
                    await _heartbeat.PingAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Unhandled error draining digest outbox");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
