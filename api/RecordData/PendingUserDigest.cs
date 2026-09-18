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
}
