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
    /// Removes the user's subscription to the given search query, if one exists.
    /// </summary>
    /// <returns>True if a subscription was removed; false if the user wasn't subscribed.</returns>
    Task<bool> UnsubscribeAsync(int userId, int searchQueryId);

    /// <summary>
    /// Every subscriber of the given search query, paired with their own digest watermark
    /// (null if no row exists yet - e.g. a pre-migration subscription the backfill missed).
    /// Called once per query during the fetch phase so the caller can compute a single floor
    /// timestamp to read records with, then filter per-subscriber in memory.
    /// </summary>
    Task<List<(int UserId, DateTime? LastDigestSentAt)>> GetUserDigestWatermarksForQueryAsync(int searchQueryId);

    /// <summary>
    /// Advances one subscriber's digest watermark for one search query (upsert). A failed send
    /// for this (user, query) pair must not affect any other subscriber of the same query, nor
    /// any other query.
    /// </summary>
    Task UpdateUserDigestWatermarkAsync(int userId, int searchQueryId, DateTime timestamp);

    /// <summary>
    /// For the given user, every OTHER search query they're subscribed to (excluding
    /// excludeSearchQueryIds) that has at least one already-persisted record newer than that
    /// user's own watermark for it. Used only by the targeted-poll sweep - never re-fetches
    /// from an external source.
    /// </summary>
    Task<List<PendingUserDigest>> GetOtherPendingDigestsForUserAsync(int userId, IReadOnlyList<int> excludeSearchQueryIds);

    /// <summary>
    /// Records that a poll of this search query's source just completed: advances the
    /// source-fetch watermark (fed back in as the next poll's ExecuteSourceSearchAsync
    /// lastRunDate - separate from UpdateLastDigestSentAtAsync, so callers should call this
    /// after any successful poll, regardless of digest outcome), and, when the source reported
    /// one, updates its current total match count (e.g. PEDro's "Found X records"). Pass null
    /// for sourceRecordCount when the source didn't report a total - the existing value is left
    /// unchanged.
    /// </summary>
    Task RecordPollCompletedAsync(int searchQueryId, DateTime polledAt, int? sourceRecordCount);
}
