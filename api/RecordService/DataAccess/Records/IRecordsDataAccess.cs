using RecordService.Models;

namespace RecordService.DataAccess;

public interface IRecordsDataAccess
{
    /// <summary>
    /// Upserts the given records and links them to the source and search query.
    /// </summary>
    /// <returns>The subset of <paramref name="records"/> newly linked to <paramref name="searchQueryId"/>.</returns>
    Task<List<LiteratureRecord>> PersistSearchResultsAsync(int searchQueryId, int sourceId, IReadOnlyList<LiteratureRecord> records);

    /// <summary>
    /// Records linked to the search query whose search_query_records.first_seen_at is after
    /// <paramref name="since"/> (or all of them, if null). This is the digest source of truth -
    /// independent of whether they were "newly linked" in the current poll - so records from a
    /// previously failed digest send are picked up again on retry.
    /// </summary>
    Task<List<LiteratureRecord>> GetRecordsSeenSinceAsync(int searchQueryId, DateTime? since);

    /// <summary>
    /// The <paramref name="topK"/> records - among only those the user tracks via their
    /// subscribed search queries - closest to <paramref name="queryEmbedding"/> by cosine distance.
    /// Records with no embedding yet are excluded.
    /// </summary>
    Task<List<LiteratureRecord>> SearchSimilarRecordsAsync(int userId, float[] queryEmbedding, int topK);
}
