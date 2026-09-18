using RecordService.Models;

namespace RecordService.BusinessLogic.RecordPollingService;

public interface IRecordPollingService
{
    /// <summary>
    /// Polls every search query for new records and persists any that are found.
    /// One search query failing does not stop the others from being processed.
    /// </summary>
    Task<List<PollResult>> PollAllSearchQueriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls a single search query for new records and persists any that are found.
    /// </summary>
    /// <returns>The poll result, or null if no search query with that ID exists.</returns>
    Task<PollResult?> PollSearchQueryAsync(int searchQueryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls exactly the given search queries (skipping any id that doesn't exist), persists
    /// new records, then dispatches one combined digest email per subscriber covering every
    /// polled query they're subscribed to with pending records - not one email per query.
    /// Also sweeps in each affected subscriber's OTHER pending queries (outside this list)
    /// from already-persisted records, without re-fetching them from an external source.
    /// </summary>
    Task<List<PollResult>> PollSearchQueriesAsync(IReadOnlyList<int> searchQueryIds, CancellationToken cancellationToken = default);
}
