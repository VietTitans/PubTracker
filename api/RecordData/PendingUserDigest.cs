namespace RecordService.Models;

/// <summary>
/// One user's pending content for one search query - what DigestService combines into a
/// single email covering every query a user has pending records for, instead of one email
/// per query. Subscriber resolution and per-user watermark filtering happen in
/// RecordPollingService before this ever reaches DigestService.
/// </summary>
public class PendingUserDigest
{
    public required int UserId { get; init; }
    public required int SearchQueryId { get; init; }
    public required string TargetUrl { get; init; }
    public required IReadOnlyList<LiteratureRecord> Records { get; init; }

    /// <summary>
    /// AI-generated summary of this query's fresh batch of records, generated once per search
    /// query per poll cycle (see RecordPollingService) rather than once per subscriber - the
    /// same query's subscribers all see the same summary text. Null if generation failed/is
    /// disabled, or this digest came from the "sweep other pending queries" path rather than a
    /// query actually polled this cycle.
    /// </summary>
    public string? Summary { get; init; }
}
