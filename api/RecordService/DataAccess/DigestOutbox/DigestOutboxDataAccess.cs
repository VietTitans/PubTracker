using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecordService.DataAccess.Entities;

namespace RecordService.DataAccess.DigestOutbox;

public class DigestOutboxDataAccess : IDigestOutboxDataAccess
{
    private readonly PubTrackerDbContext _dbContext;
    private readonly ISearchQueriesDataAccess _searchQueriesDataAccess;

    // Both data access classes are scoped over the same PubTrackerDbContext, so the claim calls
    // below join the transaction opened here.
    public DigestOutboxDataAccess(PubTrackerDbContext dbContext, ISearchQueriesDataAccess searchQueriesDataAccess)
    {
        _dbContext = dbContext;
        _searchQueriesDataAccess = searchQueriesDataAccess;
    }

    public async Task<IReadOnlyList<int>> EnqueueWithClaimsAsync(
        int userId,
        IReadOnlyList<DigestClaim> claims,
        Func<IReadOnlyList<int>, Task<(string Subject, string HtmlBody)?>> render)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        var claimed = new List<DigestClaim>();
        foreach (var claim in claims)
        {
            if (await _searchQueriesDataAccess.TryClaimUserDigestWatermarkAsync(userId, claim.SearchQueryId, claim.ExpectedOld, claim.Claimed))
            {
                claimed.Add(claim);
            }
        }

        if (claimed.Count == 0)
        {
            await transaction.RollbackAsync();
            return Array.Empty<int>();
        }

        var claimedQueryIds = claimed.Select(c => c.SearchQueryId).ToList();
        var rendered = await render(claimedQueryIds);
        if (rendered is { } digest)
        {
            var now = DateTime.UtcNow;
            _dbContext.DigestOutbox.Add(new DigestOutboxEntity
            {
                UserId = userId,
                Subject = digest.Subject,
                HtmlBody = digest.HtmlBody,
                ClaimsJson = JsonSerializer.Serialize(claimed),
                NextAttemptAt = now,
                CreatedAt = now
            });
            await _dbContext.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        return claimedQueryIds;
    }

    public async Task<List<DueDigest>> LeaseDueAsync(int batchSize, TimeSpan lease, DateTime now)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        var ids = await _dbContext.Database.SqlQuery<int>(
            $"""
             SELECT id AS "Value" FROM digest_outbox
             WHERE sent_at IS NULL AND failed_at IS NULL AND next_attempt_at <= {now}
             ORDER BY id LIMIT {batchSize} FOR UPDATE SKIP LOCKED
             """).ToListAsync();

        if (ids.Count == 0)
        {
            await transaction.RollbackAsync();
            return new List<DueDigest>();
        }

        var leaseUntil = now + lease;
        await _dbContext.DigestOutbox
            .Where(o => ids.Contains(o.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Attempts, o => o.Attempts + 1)
                .SetProperty(o => o.NextAttemptAt, leaseUntil));

        var due = await _dbContext.DigestOutbox
            .AsNoTracking()
            .Where(o => ids.Contains(o.Id))
            .OrderBy(o => o.Id)
            .Select(o => new DueDigest(o.Id, o.UserId, o.Subject, o.HtmlBody, o.Attempts))
            .ToListAsync();

        await transaction.CommitAsync();
        return due;
    }

    public async Task MarkSentAsync(int id, DateTime now)
    {
        await _dbContext.DigestOutbox
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.SentAt, now));
    }

    public async Task MarkRetryAsync(int id, DateTime nextAttemptAt, string error)
    {
        await _dbContext.DigestOutbox
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.NextAttemptAt, nextAttemptAt)
                .SetProperty(o => o.LastError, error));
    }

    public async Task MarkFailedAndReleaseClaimsAsync(int id, DateTime now, string error)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        var row = await _dbContext.DigestOutbox
            .Where(o => o.Id == id)
            .Select(o => new { o.UserId, o.ClaimsJson })
            .SingleAsync();

        foreach (var claim in JsonSerializer.Deserialize<List<DigestClaim>>(row.ClaimsJson)!)
        {
            await _searchQueriesDataAccess.ReleaseUserDigestClaimAsync(row.UserId, claim.SearchQueryId, claim.ExpectedOld, claim.Claimed);
        }

        await _dbContext.DigestOutbox
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.FailedAt, now)
                .SetProperty(o => o.LastError, error));

        await transaction.CommitAsync();
    }

    public async Task DeleteSentBeforeAsync(DateTime cutoff)
    {
        await _dbContext.DigestOutbox
            .Where(o => o.SentAt != null && o.SentAt < cutoff)
            .ExecuteDeleteAsync();
    }
}
