using RecordService.BusinessLogic.UsersService;

namespace RecordService.Workers;

/// <summary>
/// Periodically hard-deletes users whose soft-delete grace period has expired (see
/// UsersDataAccess.DeletionGracePeriodDays). Runs a cycle immediately at startup, then again
/// every <see cref="_purgeInterval"/>.
/// </summary>
public class UserPurgeBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserPurgeBackgroundService> _logger;
    private readonly TimeSpan _purgeInterval;

    public UserPurgeBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<UserPurgeBackgroundService> logger,
        TimeSpan purgeInterval)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _purgeInterval = purgeInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_purgeInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var usersService = scope.ServiceProvider.GetRequiredService<IUsersService>();
                await usersService.PurgeExpiredDeletedUsersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error during scheduled user purge cycle");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
                break;
        }
    }
}
