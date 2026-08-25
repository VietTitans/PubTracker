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
    /// Advances the search query's watermark timestamp. Also reused as the poll fetch
    /// watermark (fed back in as ExecuteSourceSearchAsync's lastRunDate), though provider
    /// implementations currently stamp DiscoveredAt at scrape time so that filter is largely
    /// a no-op in practice - persistence-level dedup (search_query_records' unique constraint)
    /// is what actually prevents re-processing already-seen records.
    /// Callers (see RecordPollingService.PollOneAsync) should only call this after a digest
    /// send has actually succeeded - records with search_query_records.first_seen_at after
    /// this timestamp are what the next poll's digest is built from, so advancing it on a
    /// failed send would silently drop those records from ever being retried.
    /// </summary>
    Task UpdateLastDigestSentAtAsync(int searchQueryId, DateTime timestamp);
}
