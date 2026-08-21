using RecordData;
using RecordService.Models;

namespace RecordService.DataAccess;

public interface ISearchQueriesDataAccess
{
    Task<SearchQuery?> GetSearchQueryByIdAsync(int searchQueryId);
    Task<List<SearchQuery>> GetAllSearchQueriesAsync();
    Task<List<int>> GetUserSubscribersForQueryAsync(int searchQueryId);

    /// <summary>
    /// Executes a search against the external literature source specified in the URL.
    /// Returns new records found since the last run.
    /// </summary>
    /// <param name="searchQueryId">The ID of the search query</param>
    /// <param name="sourceUrl">The URL of the literature source (PubMed, PEDro, etc.)</param>
    /// <param name="lastRunDate">The date of the last search execution for comparison</param>
    /// <returns>Search result containing new records count and list</returns>
    Task<SourceSearchResult> ExecuteSourceSearchAsync(int searchQueryId, string sourceUrl, DateTime? lastRunDate = null);

    /// <summary>
    /// Finds or creates the source/search query for the given URL and subscribes the user to it.
    /// </summary>
    /// <param name="userId">The ID of the subscribing user</param>
    /// <param name="targetUrl">The literature search URL to subscribe to</param>
    /// <exception cref="InvalidOperationException">Thrown if the URL doesn't match a supported literature source</exception>
    Task<SearchQuery> SubscribeAsync(int userId, string targetUrl);

    /// <summary>
    /// Advances the search query's watermark timestamp. Currently reused as the poll
    /// watermark (fed back in as ExecuteSourceSearchAsync's lastRunDate) ahead of the
    /// digest-email feature existing; may need to split into a separate column once
    /// poll cadence and digest-send cadence diverge.
    /// </summary>
    Task UpdateLastDigestSentAtAsync(int searchQueryId, DateTime timestamp);
}
