namespace RecordService.DataAccess.DigestOutbox;

/// <summary>One watermark taken for a digest; kept on the outbox row so a permanent send failure can roll it back.</summary>
public record DigestClaim(int SearchQueryId, DateTime? ExpectedOld, DateTime Claimed);

public record DueDigest(int Id, int UserId, string Subject, string HtmlBody, int Attempts);

public interface IDigestOutboxDataAccess
{
    /// <summary>
    /// In ONE transaction: claims each watermark (compare-and-swap, see
    /// <see cref="ISearchQueriesDataAccess.TryClaimUserDigestWatermarkAsync"/>) and,
    /// if any claim won, inserts one outbox row built by <paramref name="render"/> from the query ids
    /// that were actually claimed. A crash can therefore never advance a watermark without a row
    /// to send. <paramref name="render"/> returning null (nobody to send to) commits the claims
    /// without a row.
    /// </summary>
    /// <returns>Query ids whose claim won; empty if none did (nothing enqueued).</returns>
    Task<IReadOnlyList<int>> EnqueueWithClaimsAsync(
        int userId,
        IReadOnlyList<DigestClaim> claims,
        Func<IReadOnlyList<int>, Task<(string Subject, string HtmlBody)?>> render);

    /// <summary>
    /// Takes up to <paramref name="batchSize"/> due rows (FOR UPDATE SKIP LOCKED, safe across api
    /// instances), bumping attempts and pushing next_attempt_at out by <paramref name="lease"/> so a
    /// crashed worker's rows become due again after the lease.
    /// </summary>
    Task<List<DueDigest>> LeaseDueAsync(int batchSize, TimeSpan lease, DateTime now);

    Task MarkSentAsync(int id, DateTime now);

    Task MarkRetryAsync(int id, DateTime nextAttemptAt, string error);

    /// <summary>Marks the row permanently failed and releases every watermark it claimed, so the next poll re-includes those records.</summary>
    Task MarkFailedAndReleaseClaimsAsync(int id, DateTime now, string error);

    Task DeleteSentBeforeAsync(DateTime cutoff);
}
