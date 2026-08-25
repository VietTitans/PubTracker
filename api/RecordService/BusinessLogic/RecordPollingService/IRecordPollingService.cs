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
}
