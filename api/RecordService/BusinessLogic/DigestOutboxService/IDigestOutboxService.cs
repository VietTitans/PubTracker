namespace RecordService.BusinessLogic.DigestOutboxService;

public interface IDigestOutboxService
{
    /// <summary>
    /// Sends every outbox row that is due, retrying failures with backoff; after
    /// <see cref="DigestOutboxService.MaxAttempts"/> the row is failed and its watermarks released.
    /// </summary>
    /// <returns>Number of rows sent.</returns>
    Task<int> ProcessDueAsync(CancellationToken cancellationToken = default);
}
