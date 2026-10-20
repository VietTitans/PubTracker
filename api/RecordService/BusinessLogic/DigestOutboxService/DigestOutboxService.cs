using RecordService.BusinessLogic.DigestService;
using RecordService.DataAccess;
using RecordService.DataAccess.DigestOutbox;
using RecordService.DataAccess.Email;

namespace RecordService.BusinessLogic.DigestOutboxService;

public class DigestOutboxService : IDigestOutboxService
{
    public const int MaxAttempts = 5;
    private const int BatchSize = 20;
    private const int DefaultDailySendLimit = 250;
    private const int QuotaAlertPercent = 80;

    // The UTC day the quota alert last fired on. Static because this service is scoped (one instance
    // per worker pass), and the alert must fire once per day, not once per pass.
    private static DateOnly _quotaAlertedOn;
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SentRetention = TimeSpan.FromDays(30);

    private readonly IDigestOutboxDataAccess _outboxDataAccess;
    private readonly IUsersDataAccess _usersDataAccess;
    private readonly IEmailSender _emailSender;
    private readonly UnsubscribeTokens _unsubscribeTokens;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DigestOutboxService> _logger;

    public DigestOutboxService(
        IDigestOutboxDataAccess outboxDataAccess,
        IUsersDataAccess usersDataAccess,
        IEmailSender emailSender,
        UnsubscribeTokens unsubscribeTokens,
        IConfiguration configuration,
        ILogger<DigestOutboxService> logger)
    {
        _outboxDataAccess = outboxDataAccess;
        _usersDataAccess = usersDataAccess;
        _emailSender = emailSender;
        _unsubscribeTokens = unsubscribeTokens;
        _configuration = configuration;
        _logger = logger;
    }

    public int LastPassSendFailures { get; private set; }

    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken = default, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var dailyLimit = _configuration.GetValue("Email:DailySendLimit", DefaultDailySendLimit);
        var sent = 0;
        LastPassSendFailures = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            // Counts sent rows, so a restart or a second instance can't reset the day's budget.
            var sentToday = await _outboxDataAccess.CountSentSinceAsync(now.Date);
            AlertIfNearLimit(sentToday, dailyLimit, now);
            var remaining = dailyLimit - sentToday;
            if (remaining <= 0)
            {
                break;
            }

            var batch = await _outboxDataAccess.LeaseDueAsync(Math.Min(BatchSize, remaining), Lease, now);
            if (batch.Count == 0)
            {
                break;
            }

            foreach (var digest in batch)
            {
                if (await TrySendAsync(digest, now))
                {
                    sent++;
                }
            }
        }

        await _outboxDataAccess.DeleteSentBeforeAsync(now - SentRetention);
        return sent;
    }

    // A logged error is what pages the owner (Sentry alert rule), so Brevo's shared daily quota is
    // noticed before the limit is hit and Keycloak's own mails start failing.
    private void AlertIfNearLimit(int sentToday, int dailyLimit, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        if (sentToday * 100 < dailyLimit * QuotaAlertPercent || _quotaAlertedOn == today)
        {
            return;
        }

        _quotaAlertedOn = today;
        _logger.LogError(
            "Digest sending reached {Sent} of the {Limit} emails allowed per UTC day ({Percent}% alert). The Brevo daily quota is shared with Keycloak's verification and reset emails.",
            sentToday, dailyLimit, QuotaAlertPercent);
    }

    private async Task<bool> TrySendAsync(DueDigest digest, DateTime now)
    {
        try
        {
            var user = await _usersDataAccess.GetUserByIdAsync(digest.UserId);
            if (user == null || user.IsMarkedForDeletion || user.DigestsStopped)
            {
                await _outboxDataAccess.MarkSkippedAsync(digest.Id, now); // no one to send to; not a failure
                return false;
            }

            await _emailSender.SendAsync(user.Email, digest.Subject, digest.HtmlBody, _unsubscribeTokens.StopAllUrl(user.Id));
            await _outboxDataAccess.MarkSentAsync(digest.Id, now);
            return true;
        }
        catch (Exception ex)
        {
            LastPassSendFailures++;
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
