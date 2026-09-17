using RecordData;
using RecordService.BusinessLogic.RecordPollingService;
using RecordService.DataAccess;

namespace RecordService.BusinessLogic.SearchQueriesService;

public class SearchQueriesService : ISearchQueriesService
{
    private static readonly TimeSpan InitialPollDelay = TimeSpan.FromSeconds(5);

    private readonly ISearchQueriesDataAccess _dataAccess;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SearchQueriesService> _logger;

    public SearchQueriesService(ISearchQueriesDataAccess dataAccess, IServiceScopeFactory scopeFactory, ILogger<SearchQueriesService> logger)
    {
        _dataAccess = dataAccess;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<SearchQuery?> GetSearchQueryByIdAsync(int searchQueryId)
    {
        return await _dataAccess.GetSearchQueryByIdAsync(searchQueryId);
    }

    public async Task<List<int>> GetUserSubscribersForQueryAsync(int searchQueryId)
    {
        return await _dataAccess.GetUserSubscribersForQueryAsync(searchQueryId);
    }

    public async Task<SearchQuery> SubscribeAsync(int userId, string targetUrl)
    {
        var searchQuery = await _dataAccess.SubscribeAsync(userId, targetUrl);

        // New subscriptions start with zero records - a brand new search query's source has
        // never been fetched. Kick off an initial poll shortly after so the UI fills in with
        // real results instead of staying at 0 until the next scheduled poll cycle (which can
        // be up to Scheduler:PollIntervalHours away). The delay isn't about the subscribe
        // transaction (already committed by this point) - it debounces against another poll
        // of the same search query landing at nearly the same moment (e.g. a caller polling
        // right after subscribing, as the end-to-end tests do), which would otherwise race
        // this one for which poll gets credit for the newly-found records.
        _ = PollShortlyAfterSubscribeAsync(searchQuery.Id);

        return searchQuery;
    }

    private async Task PollShortlyAfterSubscribeAsync(int searchQueryId)
    {
        try
        {
            await Task.Delay(InitialPollDelay);

            using var scope = _scopeFactory.CreateScope();
            var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();
            await pollingService.PollSearchQueryAsync(searchQueryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initial poll for search query {SearchQueryId} failed", searchQueryId);
        }
    }

    public async Task<bool> UnsubscribeAsync(int userId, int searchQueryId)
    {
        return await _dataAccess.UnsubscribeAsync(userId, searchQueryId);
    }
}
