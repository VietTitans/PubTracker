namespace RecordService.BusinessLogic.DigestOutboxService;

public interface IDigestOutboxService
{
    /// <summary>
    /// Sends every outbox row that is due, retrying failures with backoff; after
    /// <see cref="DigestOutboxService.MaxAttempts"/> the row is failed and its watermarks released.
    /// </summary>
    /// <remarks>
    /// At most <c>Email:DailySendLimit</c> (default 250) rows are sent per UTC day, counted from sent
    /// rows. Rows over the limit are not leased, so they keep their attempt count and go out the next day.
    /// </remarks>
    /// <param name="utcNow">Clock override for tests; defaults to the current time.</param>
    /// <returns>Number of rows sent.</returns>
    Task<int> ProcessDueAsync(CancellationToken cancellationToken = default, DateTime? utcNow = null);
}
