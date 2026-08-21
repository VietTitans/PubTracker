using RecordService.DataAccess;
using RecordService.Models;

namespace RecordService.BusinessLogic.RecordPollingService;

public class RecordPollingService : IRecordPollingService
{
    private readonly ISearchQueriesDataAccess _searchQueriesDataAccess;
    private readonly IRecordsDataAccess _recordsDataAccess;
    private readonly ILogger<RecordPollingService> _logger;

    public RecordPollingService(
        ISearchQueriesDataAccess searchQueriesDataAccess,
        IRecordsDataAccess recordsDataAccess,
        ILogger<RecordPollingService> logger)
    {
        _searchQueriesDataAccess = searchQueriesDataAccess;
        _recordsDataAccess = recordsDataAccess;
        _logger = logger;
    }

    public async Task<List<PollResult>> PollAllSearchQueriesAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<PollResult>();
        var searchQueries = await _searchQueriesDataAccess.GetAllSearchQueriesAsync();

        foreach (var searchQuery in searchQueries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var searchResult = await _searchQueriesDataAccess.ExecuteSourceSearchAsync(
                    searchQuery.Id, searchQuery.TargetUrl, searchQuery.LastDigestSentAt);

                if (!searchResult.IsSuccessful)
                {
                    _logger.LogWarning(
                        "Search query {SearchQueryId} failed: {ErrorMessage}",
                        searchQuery.Id, searchResult.ErrorMessage);
                    results.Add(new PollResult
                    {
                        SearchQueryId = searchQuery.Id,
                        IsSuccessful = false,
                        ErrorMessage = searchResult.ErrorMessage
                    });
                    continue;
                }

                var newRecordCount = searchResult.NewRecords.Count > 0
                    ? await _recordsDataAccess.PersistSearchResultsAsync(searchQuery.Id, searchQuery.SourceId, searchResult.NewRecords)
                    : 0;

                await _searchQueriesDataAccess.UpdateLastDigestSentAtAsync(searchQuery.Id, DateTime.UtcNow);

                _logger.LogInformation(
                    "Search query {SearchQueryId}: {NewRecordCount} new / {FetchedCount} fetched",
                    searchQuery.Id, newRecordCount, searchResult.NewRecords.Count);

                results.Add(new PollResult
                {
                    SearchQueryId = searchQuery.Id,
                    IsSuccessful = true,
                    NewRecordCount = newRecordCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error polling search query {SearchQueryId}", searchQuery.Id);
                results.Add(new PollResult
                {
                    SearchQueryId = searchQuery.Id,
                    IsSuccessful = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        return results;
    }
}
