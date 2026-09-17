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
                searchQuery.Id, searchQuery.TargetUrl, searchQuery.LastPolledAt);

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

            // Advance the fetch watermark as soon as the source search itself succeeds,
            // independent of whether a digest email later succeeds or fails - otherwise a
            // provider like PEDro (whose "since" filter relies on this watermark) would
            // re-fetch the same window, or worse, never advance past its first baseline poll.
            await _searchQueriesDataAccess.RecordPollCompletedAsync(searchQuery.Id, DateTime.UtcNow, searchResult.TotalRecordCount);

            var newlyLinkedRecords = searchResult.NewRecords.Count > 0
                ? await _recordsDataAccess.PersistSearchResultsAsync(searchQuery.Id, searchQuery.SourceId, searchResult.NewRecords)
                : new List<LiteratureRecord>();

            // Digest content is the durable "seen since last successful digest" set, not just
            // this poll's newly-linked records - so records left over from a previously failed
            // send are retried alongside anything new this time.
            var pendingDigestRecords = await _recordsDataAccess.GetRecordsSeenSinceAsync(searchQuery.Id, searchQuery.LastDigestSentAt);

            var digestSucceeded = true;
            if (pendingDigestRecords.Count > 0)
            {
                try
                {
                    digestSucceeded = await _digestService.SendDigestForSearchQueryAsync(searchQuery.Id, searchQuery.TargetUrl, pendingDigestRecords);
                }
                catch (Exception ex)
                {
                    digestSucceeded = false;
                    _logger.LogWarning(ex, "Failed to send digest for search query {SearchQueryId}", searchQuery.Id);
                }
            }

            // Only advance the watermark once the digest actually went out - a failed send
            // leaves it in place so the same pending records are retried on the next poll.
            if (digestSucceeded)
            {
                await _searchQueriesDataAccess.UpdateLastDigestSentAtAsync(searchQuery.Id, DateTime.UtcNow);
            }
            else
            {
                _logger.LogWarning(
                    "Search query {SearchQueryId}: digest send failed, watermark not advanced ({PendingCount} record(s) pending retry)",
                    searchQuery.Id, pendingDigestRecords.Count);
            }

            _logger.LogInformation(
                "Search query {SearchQueryId}: {NewRecordCount} new / {FetchedCount} fetched",
                searchQuery.Id, newlyLinkedRecords.Count, searchResult.NewRecords.Count);

            return new PollResult
            {
                SearchQueryId = searchQuery.Id,
                IsSuccessful = digestSucceeded,
                NewRecordCount = newlyLinkedRecords.Count,
                ErrorMessage = digestSucceeded ? null : "Digest send failed; will retry on next poll."
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
