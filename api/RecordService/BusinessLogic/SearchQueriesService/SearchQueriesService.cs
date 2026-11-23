using RecordData;
using RecordService.Authentication;
using RecordService.Exceptions;
using RecordService.BusinessLogic.RecordPollingService;
using RecordService.DataAccess;

namespace RecordService.BusinessLogic.SearchQueriesService;

public class SearchQueriesService : ISearchQueriesService
{
    private readonly TimeSpan _initialPollDelay;
    private readonly ISearchQueriesDataAccess _dataAccess;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SearchQueriesService> _logger;

    public SearchQueriesService(ISearchQueriesDataAccess dataAccess, IServiceScopeFactory scopeFactory, ILogger<SearchQueriesService> logger, IConfiguration configuration)
    {
        // Configurable so tests can park the background poll instead of racing their own polls.
        _initialPollDelay = TimeSpan.FromSeconds(configuration.GetValue("Scheduler:InitialPollDelaySeconds", 5));
        _dataAccess = dataAccess;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<SearchQuery?> GetSearchQueryByIdAsync(Caller caller, int searchQueryId)
    {
        if (caller.IsAdmin)
        {
            caller.RequireAdmin();
        }
        else if (caller.UserId <= 0 || !(await _dataAccess.GetUserSubscribersForQueryAsync(searchQueryId)).Contains(caller.UserId))
        {
            // Forbidden whether or not the query exists, so a non-subscriber learns nothing.
            throw new ForbiddenException();
        }
        return await _dataAccess.GetSearchQueryByIdAsync(searchQueryId);
    }

    public async Task<Paged<int>> GetUserSubscribersPageForQueryAsync(Caller caller, int searchQueryId, int page, int pageSize)
    {
        caller.RequireAdmin();
        return await _dataAccess.GetUserSubscribersPageForQueryAsync(searchQueryId, page, pageSize);
    }

    public async Task<SearchQuery> SubscribeAsync(Caller caller, int userId, string targetUrl)
    {
        caller.RequireSelf(userId);
        var searchQuery = await _dataAccess.SubscribeAsync(userId, targetUrl);

        // New subscriptions start with zero records; a brand new search query's source has
        // never been fetched. Kick off an initial poll shortly after so the UI fills in with
        // real results instead of staying at 0 until the next scheduled poll cycle (which can
        // be up to Scheduler:PollIntervalHours away). The delay isn't about the subscribe
        // transaction (already committed by this point); it debounces against another poll
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
            await Task.Delay(_initialPollDelay);

            using var scope = _scopeFactory.CreateScope();
            var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();
            await pollingService.PollSearchQueryAsync(searchQueryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initial poll for search query {SearchQueryId} failed", searchQueryId);
        }
    }

    public async Task<bool> UnsubscribeAsync(Caller caller, int userId, int searchQueryId)
    {
        caller.RequireSelf(userId);
        return await _dataAccess.UnsubscribeAsync(userId, searchQueryId);
    }
}
