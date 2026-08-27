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
}
