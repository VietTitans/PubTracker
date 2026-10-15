using RecordService.Models;

namespace RecordService.DataAccess;

public interface IRecordsDataAccess
{
    /// <summary>
    /// Upserts the given records and links them to the source and search query.
    /// </summary>
    Task<PersistResult> PersistSearchResultsAsync(int searchQueryId, int sourceId, IReadOnlyList<LiteratureRecord> records);

    /// <summary>
    /// Newest-first records that have an abstract but no author intention yet (null, not the
    /// empty string, which means "attempted, nothing to show"), at most <paramref name="limit"/>.
    /// Not tied to any search query, so it also reaches records no poll will return again.
    /// </summary>
    Task<List<LiteratureRecord>> GetRecordsMissingAuthorIntentionAsync(int limit);

    /// <summary>Sets the cached author-intention text for the record with the given external id.</summary>
    Task UpdateAuthorIntentionAsync(string externalId, string authorIntention);

    /// <summary>
    /// Records linked to the search query whose search_query_records.first_seen_at is after
    /// <paramref name="since"/> (or all of them, if null). This is the digest source of truth -
    /// independent of whether they were "newly linked" in the current poll; so records from a
    /// previously failed digest send are picked up again on retry.
    /// </summary>
    Task<List<LiteratureRecord>> GetRecordsSeenSinceAsync(int searchQueryId, DateTime? since);
}
