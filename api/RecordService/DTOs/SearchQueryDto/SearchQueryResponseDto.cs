namespace RecordService.DTOs.SearchQueryDto;

public class SearchQueryResponseDto
{
    public int Id { get; set; }

    public int SourceId { get; set; }

    public string TargetUrl { get; set; } = string.Empty;

    public List<string>? Subscribers { get; set; }

    public DateTime? LastDigestSentAt { get; set; }

    public int RecordCount { get; set; }

    public DateTime? LastFetchedAt { get; set; }

    /// <summary>
    /// The source's current total match count for this search (e.g. PEDro's "Found X
    /// records"), as of the most recent successful poll. Null until the first successful poll.
    /// </summary>
    public int? SourceRecordCount { get; set; }

    /// <summary>
    /// Ready-to-display tags describing TargetUrl's search, derived per-source (see
    /// PedroDigestMessageBuilder.GetKeywordTags and PubMedDigestMessageBuilder.GetKeywordTags).
    /// Empty for an unrecognized source or a URL with no recognized search fields.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// When this query's source was last found unreachable, so the frontend can show a quiet
    /// "retrying automatically" status; null whenever the most recent poll succeeded. This is
    /// deliberately just a timestamp, not the underlying error text: that can contain internal
    /// details (exception messages, hostnames) that shouldn't reach the browser.
    /// </summary>
    public DateTime? LastPollFailedAt { get; set; }
}
