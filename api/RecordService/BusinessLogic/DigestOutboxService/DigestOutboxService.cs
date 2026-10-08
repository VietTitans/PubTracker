using RecordService.DataAccess;
using RecordService.DataAccess.DigestOutbox;
using RecordService.DataAccess.Email;

namespace RecordService.BusinessLogic.DigestOutboxService;

public class DigestOutboxService : IDigestOutboxService
{
    public const int MaxAttempts = 5;
    private const int BatchSize = 20;
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SentRetention = TimeSpan.FromDays(30);

    private readonly IDigestOutboxDataAccess _outboxDataAccess;
    private readonly IUsersDataAccess _usersDataAccess;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<DigestOutboxService> _logger;

    public DigestOutboxService(
        IDigestOutboxDataAccess outboxDataAccess,
        IUsersDataAccess usersDataAccess,
        IEmailSender emailSender,
        ILogger<DigestOutboxService> logger)
    {
        _outboxDataAccess = outboxDataAccess;
        _usersDataAccess = usersDataAccess;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        var sent = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await _outboxDataAccess.LeaseDueAsync(BatchSize, Lease, DateTime.UtcNow);
            if (batch.Count == 0)
            {
                break;
            }

            foreach (var digest in batch)
            {
                if (await TrySendAsync(digest))
                {
                    sent++;
                }
            }
        }

        await _outboxDataAccess.DeleteSentBeforeAsync(DateTime.UtcNow - SentRetention);
        return sent;
    }

    private async Task<bool> TrySendAsync(DueDigest digest)
    {
        try
        {
            var user = await _usersDataAccess.GetUserByIdAsync(digest.UserId);
            if (user == null || user.IsMarkedForDeletion)
            {
                await _outboxDataAccess.MarkSentAsync(digest.Id, DateTime.UtcNow); // no one to send to; not a failure
                return false;
            }

            await _emailSender.SendAsync(user.Email, digest.Subject, digest.HtmlBody);
            await _outboxDataAccess.MarkSentAsync(digest.Id, DateTime.UtcNow);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Digest outbox {OutboxId} send failed (attempt {Attempts}/{MaxAttempts})", digest.Id, digest.Attempts, MaxAttempts);
            try
            {
                if (digest.Attempts >= MaxAttempts)
                {
                    await _outboxDataAccess.MarkFailedAndReleaseClaimsAsync(digest.Id, DateTime.UtcNow, ex.Message);
                    _logger.LogError("Digest outbox {OutboxId} failed permanently; watermarks released for the next poll", digest.Id);
                }
                else
                {
                    var backoff = TimeSpan.FromMinutes(Math.Pow(2, digest.Attempts));
                    await _outboxDataAccess.MarkRetryAsync(digest.Id, DateTime.UtcNow + backoff, ex.Message);
                }
            }
            catch (Exception markEx)
            {
                // Row stays leased; it becomes due again when the lease expires.
                _logger.LogError(markEx, "Could not record failure for digest outbox {OutboxId}", digest.Id);
            }
            return false;
        }
    }
}
