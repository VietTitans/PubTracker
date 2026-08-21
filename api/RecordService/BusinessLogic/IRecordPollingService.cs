using RecordService.Models;

namespace RecordService.BusinessLogic;

public interface IRecordPollingService
{
    /// <summary>
    /// Polls every search query for new records and persists any that are found.
    /// One search query failing does not stop the others from being processed.
    /// </summary>
    Task<List<PollResult>> PollAllSearchQueriesAsync(CancellationToken cancellationToken = default);
}
