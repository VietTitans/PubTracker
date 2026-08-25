using RecordData;
using RecordService.BusinessLogic.DigestService;
using RecordService.DataAccess;
using RecordService.Models;

namespace RecordService.BusinessLogic.RecordPollingService;

public class RecordPollingService : IRecordPollingService
{
    private readonly ISearchQueriesDataAccess _searchQueriesDataAccess;
    private readonly IRecordsDataAccess _recordsDataAccess;
    private readonly IDigestService _digestService;
    private readonly ILogger<RecordPollingService> _logger;

    public RecordPollingService(
        ISearchQueriesDataAccess searchQueriesDataAccess,
        IRecordsDataAccess recordsDataAccess,
        IDigestService digestService,
        ILogger<RecordPollingService> logger)
    {
        _searchQueriesDataAccess = searchQueriesDataAccess;
        _recordsDataAccess = recordsDataAccess;
        _digestService = digestService;
        _logger = logger;
    }

    public async Task<List<PollResult>> PollAllSearchQueriesAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<PollResult>();
        var searchQueries = await _searchQueriesDataAccess.GetAllSearchQueriesAsync();

        foreach (var searchQuery in searchQueries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await PollOneAsync(searchQuery));
        }

        return results;
    }

    public async Task<PollResult?> PollSearchQueryAsync(int searchQueryId, CancellationToken cancellationToken = default)
    {
        var searchQuery = await _searchQueriesDataAccess.GetSearchQueryByIdAsync(searchQueryId);
        if (searchQuery == null)
        {
            return null;
        }

        return await PollOneAsync(searchQuery);
    }

    private async Task<PollResult> PollOneAsync(SearchQuery searchQuery)
    {
        try
        {
            var searchResult = await _searchQueriesDataAccess.ExecuteSourceSearchAsync(
                searchQuery.Id, searchQuery.TargetUrl, searchQuery.LastDigestSentAt);

            if (!searchResult.IsSuccessful)
            {
                _logger.LogWarning(
                    "Search query {SearchQueryId} failed: {ErrorMessage}",
                    searchQuery.Id, searchResult.ErrorMessage);
                return new PollResult
                {
                    SearchQueryId = searchQuery.Id,
                    IsSuccessful = false,
                    ErrorMessage = searchResult.ErrorMessage
                };
            }

            var newlyLinkedRecords = searchResult.NewRecords.Count > 0
                ? await _recordsDataAccess.PersistSearchResultsAsync(searchQuery.Id, searchQuery.SourceId, searchResult.NewRecords)
                : new List<LiteratureRecord>();

            if (newlyLinkedRecords.Count > 0)
            {
                try
                {
                    await _digestService.SendDigestForSearchQueryAsync(searchQuery.Id, searchQuery.TargetUrl, newlyLinkedRecords);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send digest for search query {SearchQueryId}", searchQuery.Id);
                }
            }

            await _searchQueriesDataAccess.UpdateLastDigestSentAtAsync(searchQuery.Id, DateTime.UtcNow);

            _logger.LogInformation(
                "Search query {SearchQueryId}: {NewRecordCount} new / {FetchedCount} fetched",
                searchQuery.Id, newlyLinkedRecords.Count, searchResult.NewRecords.Count);

            return new PollResult
            {
                SearchQueryId = searchQuery.Id,
                IsSuccessful = true,
                NewRecordCount = newlyLinkedRecords.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error polling search query {SearchQueryId}", searchQuery.Id);
            return new PollResult
            {
                SearchQueryId = searchQuery.Id,
                IsSuccessful = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
