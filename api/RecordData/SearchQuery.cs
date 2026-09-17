namespace RecordData;
public class SearchQuery
{
    public int Id { get; set; }

    public int SourceId { get; set; }
    public string TargetUrl { get; set; } = string.Empty;
    public List<string>? Subscribers { get; set; }
    public DateTime? LastDigestSentAt { get; set; }

    /// <summary>
    /// Count of records linked to this search query via search_query_records. Populated by
    /// GetSearchQueryByIdAsync and GetSearchQueriesByUserAsync via a correlated subquery;
    /// other fetch paths (e.g. GetAllSearchQueriesAsync, used by the polling worker) leave
    /// this at its default 0 since they don't need it.
    /// </summary>
    public int RecordCount { get; set; }

    /// <summary>
    /// When this search query's source was last successfully fetched, derived from
    /// MAX(search_query_records.first_seen_at) - distinct from LastDigestSentAt, which only
    /// advances once an email has actually been sent. A fetch can succeed (and this can be
    /// set) even while the digest send keeps failing. Populated by the same data access
    /// methods as RecordCount; null if never fetched.
    /// </summary>
    public DateTime? LastFetchedAt { get; set; }

    /// <summary>
    /// When this search query's source was last successfully polled, regardless of whether
    /// that poll found or persisted anything - fed back into ExecuteSourceSearchAsync as the
    /// next poll's lastRunDate. Deliberately separate from LastDigestSentAt: a provider like
    /// PEDro (which returns nothing but a baseline total on its very first poll, to avoid
    /// pulling full details for a potentially huge backlog) still needs its "fetch new records
    /// created since X" filter to advance, even on a poll where there was nothing to email.
    /// </summary>
    public DateTime? LastPolledAt { get; set; }

    /// <summary>
    /// The source's current total match count for this search (e.g. PEDro's "Found X
    /// records"), as of the most recent successful poll - distinct from RecordCount, which is
    /// only how many of those have actually been fetched and linked so far. Null until the
    /// first successful poll.
    /// </summary>
    public int? SourceRecordCount { get; set; }
}
