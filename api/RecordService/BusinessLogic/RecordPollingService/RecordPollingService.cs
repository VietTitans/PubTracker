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
        var searchQueries = await _searchQueriesDataAccess.GetAllSearchQueriesAsync();
        var outcomes = new List<FetchOutcome>();

        foreach (var searchQuery in searchQueries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            outcomes.Add(await FetchOneAsync(searchQuery));
        }

        return await DispatchDigestsAndBuildResultsAsync(outcomes, explicitSearchQueryIds: null);
    }

    public async Task<List<PollResult>> PollSearchQueriesAsync(IReadOnlyList<int> searchQueryIds, CancellationToken cancellationToken = default)
    {
        var outcomes = new List<FetchOutcome>();

        foreach (var id in searchQueryIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var searchQuery = await _searchQueriesDataAccess.GetSearchQueryByIdAsync(id);
            if (searchQuery == null)
            {
                continue;
            }
            outcomes.Add(await FetchOneAsync(searchQuery));
        }

        return await DispatchDigestsAndBuildResultsAsync(outcomes, explicitSearchQueryIds: searchQueryIds);
    }

    public async Task<PollResult?> PollSearchQueryAsync(int searchQueryId, CancellationToken cancellationToken = default)
    {
        var results = await PollSearchQueriesAsync(new[] { searchQueryId }, cancellationToken);
        return results.Count > 0 ? results[0] : null;
    }

    // Everything through computing pending records, for one search query - no email is sent
    // here. Dispatch is deferred until every requested query has been fetched, so digests can
    // be combined per user instead of sent one-per-query.
    private sealed class FetchOutcome
    {
        public required SearchQuery SearchQuery { get; init; }
        public bool FetchSucceeded { get; init; }
        public string? ErrorMessage { get; init; }
        public int NewlyLinkedCount { get; init; }
        public List<LiteratureRecord> FetchedPendingRecords { get; init; } = new(); // superset across all subscribers
        public List<(int UserId, DateTime? LastDigestSentAt)> SubscriberWatermarks { get; init; } = new();

        // Captured right before the read, not "now" at dispatch time (which can be much later
        // once combining across many users) - stamping this as the new watermark on success
        // guarantees it never exceeds what this read actually saw.
        public DateTime FetchReadAt { get; init; }
    }

    private async Task<FetchOutcome> FetchOneAsync(SearchQuery searchQuery)
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
                return new FetchOutcome
                {
                    SearchQuery = searchQuery,
                    FetchSucceeded = false,
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

            var subscriberWatermarks = await _searchQueriesDataAccess.GetUserDigestWatermarksForQueryAsync(searchQuery.Id);

            var fetchedPendingRecords = new List<LiteratureRecord>();
            var fetchReadAt = DateTime.UtcNow;
            if (subscriberWatermarks.Count > 0)
            {
                // Read from the earliest watermark across all subscribers (or from the very
                // beginning if anyone has never been sent a digest for this query yet), then
                // each subscriber's own pending set is sliced out of this superset in memory.
                var anyNeverSent = subscriberWatermarks.Any(w => w.LastDigestSentAt == null);
                var floorWatermark = anyNeverSent ? (DateTime?)null : subscriberWatermarks.Min(w => w.LastDigestSentAt);
                fetchedPendingRecords = await _recordsDataAccess.GetRecordsSeenSinceAsync(searchQuery.Id, floorWatermark);
            }

            _logger.LogInformation(
                "Search query {SearchQueryId}: {NewRecordCount} new / {FetchedCount} fetched",
                searchQuery.Id, newlyLinkedRecords.Count, searchResult.NewRecords.Count);

            return new FetchOutcome
            {
                SearchQuery = searchQuery,
                FetchSucceeded = true,
                NewlyLinkedCount = newlyLinkedRecords.Count,
                FetchedPendingRecords = fetchedPendingRecords,
                SubscriberWatermarks = subscriberWatermarks,
                FetchReadAt = fetchReadAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error polling search query {SearchQueryId}", searchQuery.Id);
            return new FetchOutcome { SearchQuery = searchQuery, FetchSucceeded = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<List<PollResult>> DispatchDigestsAndBuildResultsAsync(
        IReadOnlyList<FetchOutcome> outcomes, IReadOnlyList<int>? explicitSearchQueryIds)
    {
        // 1. Per-(user, query) pending content from this cycle's fresh fetches, filtered
        //    against each subscriber's own watermark.
        var pendingByUserAndQuery = new Dictionary<(int UserId, int SearchQueryId), (PendingUserDigest Digest, DateTime WatermarkToStamp)>();

        foreach (var outcome in outcomes.Where(o => o.FetchSucceeded))
        {
            foreach (var (userId, lastSentAt) in outcome.SubscriberWatermarks)
            {
                var effectiveSince = lastSentAt ?? DateTime.MinValue;
                var recordsForUser = outcome.FetchedPendingRecords.Where(r => r.FirstSeenAt > effectiveSince).ToList();
                if (recordsForUser.Count == 0)
                {
                    continue;
                }

                pendingByUserAndQuery[(userId, outcome.SearchQuery.Id)] = (
                    new PendingUserDigest
                    {
                        UserId = userId,
                        SearchQueryId = outcome.SearchQuery.Id,
                        TargetUrl = outcome.SearchQuery.TargetUrl,
                        Records = recordsForUser
                    },
                    outcome.FetchReadAt);
            }
        }

        // 2. Targeted-poll only: sweep each affected subscriber's OTHER queries for
        //    already-persisted, already-pending records, without re-fetching from any
        //    external source.
        if (explicitSearchQueryIds is { Count: > 0 })
        {
            var affectedUserIds = outcomes.Where(o => o.FetchSucceeded)
                .SelectMany(o => o.SubscriberWatermarks.Select(w => w.UserId))
                .Distinct();

            foreach (var userId in affectedUserIds)
            {
                var sweepReadAt = DateTime.UtcNow;
                var otherPending = await _searchQueriesDataAccess.GetOtherPendingDigestsForUserAsync(userId, explicitSearchQueryIds);
                foreach (var pending in otherPending)
                {
                    pendingByUserAndQuery[(userId, pending.SearchQueryId)] = (pending, sweepReadAt);
                }
            }
        }

        var successByKey = pendingByUserAndQuery.Count > 0
            ? await TrySendCombinedDigestsAsync(pendingByUserAndQuery.Values.Select(v => v.Digest).ToList())
            : new Dictionary<(int, int), bool>();

        foreach (var ((userId, searchQueryId), (_, watermarkToStamp)) in pendingByUserAndQuery)
        {
            if (successByKey[(userId, searchQueryId)])
            {
                await _searchQueriesDataAccess.UpdateUserDigestWatermarkAsync(userId, searchQueryId, watermarkToStamp);
            }
            else
            {
                _logger.LogWarning(
                    "User {UserId}, search query {SearchQueryId}: digest send failed, watermark not advanced",
                    userId, searchQueryId);
            }
        }

        return BuildResults(outcomes, successByKey);
    }

    private async Task<Dictionary<(int, int), bool>> TrySendCombinedDigestsAsync(IReadOnlyList<PendingUserDigest> pendingDigests)
    {
        try
        {
            return (await _digestService.SendCombinedDigestsAsync(pendingDigests)).ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send combined digests for {Count} (user, query) pair(s)", pendingDigests.Count);
            return pendingDigests.ToDictionary(p => (p.UserId, p.SearchQueryId), _ => false);
        }
    }

    private static List<PollResult> BuildResults(
        IReadOnlyList<FetchOutcome> outcomes, IReadOnlyDictionary<(int UserId, int SearchQueryId), bool> successByKey)
    {
        var results = new List<PollResult>();
        foreach (var outcome in outcomes)
        {
            if (!outcome.FetchSucceeded)
            {
                results.Add(new PollResult
                {
                    SearchQueryId = outcome.SearchQuery.Id,
                    IsSuccessful = false,
                    ErrorMessage = outcome.ErrorMessage
                });
                continue;
            }

            var relevant = successByKey.Where(kv => kv.Key.SearchQueryId == outcome.SearchQuery.Id).Select(kv => kv.Value).ToList();
            var digestSucceeded = relevant.All(v => v); // vacuously true if nobody had anything pending
            var failedCount = relevant.Count(v => !v);

            results.Add(new PollResult
            {
                SearchQueryId = outcome.SearchQuery.Id,
                IsSuccessful = digestSucceeded,
                NewRecordCount = outcome.NewlyLinkedCount,
                ErrorMessage = digestSucceeded ? null : $"Digest send failed for {failedCount} subscriber(s); they'll be retried on next poll."
            });
        }
        return results;
    }
}
