using RecordService.BusinessLogic.RecordPollingService;
using RecordService.DataAccess;

namespace RecordService.Workers;

/// <summary>
/// Periodically polls every search query for new records. Runs a cycle immediately at
/// startup, then again every <see cref="_pollInterval"/>.
/// </summary>
public class RecordPollingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecordPollingBackgroundService> _logger;
    private readonly TimeSpan _pollInterval;

    public RecordPollingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<RecordPollingBackgroundService> logger,
        TimeSpan pollInterval)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pollInterval = pollInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Restart-safe: don't re-poll everything on every restart. Wait until the oldest query
        // is a full interval old (a never-polled query, or no queries, means due now). Applies
        // to the first cycle only; later cycles follow the timer.
        try
        {
            using var gateScope = _scopeFactory.CreateScope();
            var oldest = await gateScope.ServiceProvider
                .GetRequiredService<ISearchQueriesDataAccess>()
                .GetOldestLastPolledAtAsync();
            var wait = oldest is null ? TimeSpan.Zero : oldest.Value + _pollInterval - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                _logger.LogInformation("First poll cycle deferred by {Wait}", wait);
                await Task.Delay(wait, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not evaluate poll gate; polling immediately");
        }

        using var timer = new PeriodicTimer(_pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();
                var results = await pollingService.PollAllSearchQueriesAsync(stoppingToken);

                _logger.LogInformation(
                    "Poll cycle complete: {QueryCount} queries, {NewCount} new records",
                    results.Count, results.Sum(r => r.NewRecordCount));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during scheduled poll cycle");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
                break;
        }
    }
}
