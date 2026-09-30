using RecordData;
using RecordService.BusinessLogic.DigestService;
using RecordService.DataAccess;
using RecordService.DataAccess.Summarization;
using RecordService.Models;

namespace RecordService.BusinessLogic.RecordPollingService;

public class RecordPollingService : IRecordPollingService
{
    // Caps how many GenerateAuthorIntentionAsync calls run concurrently - bounded so a
    // large batch of newly-inserted records doesn't blow through the LLM provider's rate limit.
    private const int MaxConcurrentAuthorIntentionCalls = 5;

    // Serializes every poll cycle (scheduled or targeted) so two overlapping calls - e.g. the
    // scheduled background service firing at the same moment as a manual re-check - can't both
    // read the same stale digest watermark and both send a duplicate digest. Static (not an
    // instance field) because this service is registered scoped, but the lock must be shared
    // across every scope/request in the process.
    // The Postgres advisory lock (_advisoryLock) extends that across processes - a second `api`
    // instance or an overlapping rolling deploy. The semaphore stays as the in-process queue.
    private static readonly SemaphoreSlim PollCycleLock = new(1, 1);

    private readonly ISearchQueriesDataAccess _searchQueriesDataAccess;
    private readonly IRecordsDataAccess _recordsDataAccess;
    private readonly IDigestService _digestService;
    private readonly ISummaryGenerator _summaryGenerator;
    private readonly PollCycleAdvisoryLock _advisoryLock;
    private readonly ILogger<RecordPollingService> _logger;

    public RecordPollingService(
        ISearchQueriesDataAccess searchQueriesDataAccess,
        IRecordsDataAccess recordsDataAccess,
        IDigestService digestService,
        ISummaryGenerator summaryGenerator,
        PollCycleAdvisoryLock advisoryLock,
        ILogger<RecordPollingService> logger)
    {
        _searchQueriesDataAccess = searchQueriesDataAccess;
        _recordsDataAccess = recordsDataAccess;
        _digestService = digestService;
        _summaryGenerator = summaryGenerator;
        _advisoryLock = advisoryLock;
        _logger = logger;
    }

    public async Task<List<PollResult>> PollAllSearchQueriesAsync(CancellationToken cancellationToken = default)
    {
        await PollCycleLock.WaitAsync(cancellationToken);
        try
        {
            // Scheduled cycle: if another instance is already polling, skip the current cycle
            var held = await _advisoryLock.TryAcquireAsync(cancellationToken);
            if (held == null)
            {
                _logger.LogInformation("Poll cycle skipped: another instance holds the poll lock");
                return new List<PollResult>();
            }
            await using var _ = held;

            var searchQueries = await _searchQueriesDataAccess.GetAllSearchQueriesAsync();
            var outcomes = new List<FetchOutcome>();

            foreach (var searchQuery in searchQueries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                outcomes.Add(await FetchOneAsync(searchQuery));
            }

            return await DispatchDigestsAndBuildResultsAsync(outcomes, explicitSearchQueryIds: null);
        }
        finally
        {
            PollCycleLock.Release();
        }
    }

    public async Task<List<PollResult>> PollSearchQueriesAsync(IReadOnlyList<int> searchQueryIds, CancellationToken cancellationToken = default)
    {
        await PollCycleLock.WaitAsync(cancellationToken);
        try
        {
            // Manual re-check: wait for any in-flight cycle (this or another instance) instead of failing.
            await using var _ = await _advisoryLock.AcquireAsync(cancellationToken);

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
        finally
        {
            PollCycleLock.Release();
        }
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

        // AI summary of this cycle's fresh batch (searchResult.NewRecords), generated once per
        // query rather than once per subscriber - every subscriber of this query sees the same
        // text. Null if there was nothing new, no one subscribed, or generation failed.
        public string? Summary { get; init; }

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
                await _searchQueriesDataAccess.RecordPollFailedAsync(searchQuery.Id, DateTime.UtcNow);
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

            var persistResult = searchResult.NewRecords.Count > 0
                ? await _recordsDataAccess.PersistSearchResultsAsync(searchQuery.Id, searchQuery.SourceId, searchResult.NewRecords)
                : new PersistResult { NewlyLinkedRecords = new(), NewlyInsertedRecords = new() };

            await GenerateAuthorIntentionsAsync(persistResult.NewlyInsertedRecords);

            var newlyLinkedRecords = persistResult.NewlyLinkedRecords;

            var subscriberWatermarks = await _searchQueriesDataAccess.GetUserDigestWatermarksForQueryAsync(searchQuery.Id);

            var fetchedPendingRecords = new List<LiteratureRecord>();
            var fetchReadAt = DateTime.UtcNow;
            string? summary = null;
            if (subscriberWatermarks.Count > 0)
            {
                // Read from the earliest watermark across all subscribers (or from the very
                // beginning if anyone has never been sent a digest for this query yet), then
                // each subscriber's own pending set is sliced out of this superset in memory.
                var anyNeverSent = subscriberWatermarks.Any(w => w.LastDigestSentAt == null);
                var floorWatermark = anyNeverSent ? (DateTime?)null : subscriberWatermarks.Min(w => w.LastDigestSentAt);
                fetchedPendingRecords = await _recordsDataAccess.GetRecordsSeenSinceAsync(searchQuery.Id, floorWatermark);

                if (searchResult.NewRecords.Count > 0)
                {
                    summary = await GenerateQuerySummaryAsync(searchQuery.Id, searchResult.NewRecords);
                }
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
                FetchReadAt = fetchReadAt,
                Summary = summary
            };
        }
        catch (Exception ex)
        {
            // Deliberately does NOT call RecordPollFailedAsync - everything in this try block
            // past ExecuteSourceSearchAsync is our own DB access, so an exception here (e.g. a
            // transient Postgres error) is not "the source is unreachable", and that write would
            // likely fail the same way anyway, which would abort this foreach and skip every
            // remaining query in the cycle.
            _logger.LogError(ex, "Unexpected error polling search query {SearchQueryId}", searchQuery.Id);
            return new FetchOutcome { SearchQuery = searchQuery, FetchSucceeded = false, ErrorMessage = ex.Message };
        }
    }

    // Runs once per query per poll cycle (not once per subscriber), so every subscriber of this
    // query gets the same summary text instead of paying for the same content N times. A
    // generation failure is logged and skipped - the digest still sends without one.
    private async Task<string?> GenerateQuerySummaryAsync(int searchQueryId, IReadOnlyList<LiteratureRecord> newRecords)
    {
        try
        {
            var summary = await _summaryGenerator.SummarizeAsync(newRecords);
            return string.IsNullOrWhiteSpace(summary) ? null : summary;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate digest summary for search query {SearchQueryId}", searchQueryId);
            return null;
        }
    }

    // Runs once per genuinely new record (not per subscriber, not per digest send - see
    // RecordsDataAccess.PersistSearchResultsAsync's xmax = 0 check for how "genuinely new" is
    // determined), so the same record is never re-summarized just because a second search query
    // later links to it too. A generation failure is logged and skipped, never blocks the poll.
    //
    // The LLM calls run concurrently (bounded by MaxConcurrentAuthorIntentionCalls) since they
    // only hit HttpClient, which is safe for concurrent use. The resulting DB writes then run
    // sequentially afterward, since _recordsDataAccess shares one scoped PubTrackerDbContext
    // (EF Core's DbContext is not safe for concurrent use) - this still gets nearly the whole
    // latency win, since the LLM round-trips are what dominate (~1s each vs. a single-row update).
    private async Task GenerateAuthorIntentionsAsync(IReadOnlyList<LiteratureRecord> newlyInsertedRecords)
    {
        if (newlyInsertedRecords.Count == 0)
        {
            return;
        }

        using var throttle = new SemaphoreSlim(MaxConcurrentAuthorIntentionCalls);

        var intentions = await Task.WhenAll(newlyInsertedRecords.Select(async record =>
        {
            await throttle.WaitAsync();
            try
            {
                return (record.ExternalId, Intention: await _summaryGenerator.GenerateAuthorIntentionAsync(record));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate author intention for record {ExternalId}", record.ExternalId);
                return (record.ExternalId, Intention: (string?)null);
            }
            finally
            {
                throttle.Release();
            }
        }));

        foreach (var (externalId, intention) in intentions)
        {
            if (!string.IsNullOrWhiteSpace(intention))
            {
                await _recordsDataAccess.UpdateAuthorIntentionAsync(externalId, intention);
            }
        }
    }

    private async Task<List<PollResult>> DispatchDigestsAndBuildResultsAsync(
        IReadOnlyList<FetchOutcome> outcomes, IReadOnlyList<int>? explicitSearchQueryIds)
    {
        // 1. Per-(user, query) pending content from this cycle's fresh fetches, filtered
        //    against each subscriber's own watermark.
        var pendingByUserAndQuery = new Dictionary<(int UserId, int SearchQueryId), (PendingUserDigest Digest, DateTime WatermarkToStamp, DateTime? ExpectedOld)>();

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
                        Records = recordsForUser,
                        Summary = outcome.Summary
                    },
                    outcome.FetchReadAt,
                    lastSentAt);
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
                    pendingByUserAndQuery[(userId, pending.SearchQueryId)] = (pending, sweepReadAt, pending.LastDigestSentAt);
                }
            }
        }

        // 3. Claim each watermark atomically BEFORE sending, so only the poll that wins the
        //    claim sends. A lost claim (another poll already advanced it) is dropped - its
        //    digest is that other poll's job.
        //    ponytail: at-most-once on crash - if the process dies between claim and send, that
        //    digest is lost (watermark already advanced). Add a claim lease column if that matters.
        var claimed = new Dictionary<(int UserId, int SearchQueryId), (PendingUserDigest Digest, DateTime WatermarkToStamp, DateTime? ExpectedOld)>();
        foreach (var (key, value) in pendingByUserAndQuery)
        {
            if (await _searchQueriesDataAccess.TryClaimUserDigestWatermarkAsync(
                    key.UserId, key.SearchQueryId, value.ExpectedOld, value.WatermarkToStamp))
            {
                claimed[key] = value;
            }
            else
            {
                _logger.LogInformation(
                    "User {UserId}, search query {SearchQueryId}: watermark already claimed by another poll, skipping send",
                    key.UserId, key.SearchQueryId);
            }
        }

        var successByKey = claimed.Count > 0
            ? await TrySendCombinedDigestsAsync(claimed.Values.Select(v => v.Digest).ToList())
            : new Dictionary<(int, int), bool>();

        foreach (var ((userId, searchQueryId), (_, watermarkToStamp, expectedOld)) in claimed)
        {
            if (!successByKey[(userId, searchQueryId)])
            {
                await _searchQueriesDataAccess.ReleaseUserDigestClaimAsync(userId, searchQueryId, expectedOld, watermarkToStamp);
                _logger.LogWarning(
                    "User {UserId}, search query {SearchQueryId}: digest send failed, watermark rolled back",
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
