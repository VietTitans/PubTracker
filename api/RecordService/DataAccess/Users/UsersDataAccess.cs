using Microsoft.EntityFrameworkCore;
using RecordData;
using RecordService.DataAccess.Entities;

namespace RecordService.DataAccess;

public class UsersDataAccess : IUsersDataAccess
{
    // How long a soft-deleted account can be revived by signing back in before the purge
    // worker (see UserPurgeBackgroundService) hard-deletes it for good.
    private const int DeletionGracePeriodDays = 30;
    // An account nobody has signed in to for this long is warned, then purged after DeletionGracePeriodDays.
    private const int InactivityYears = 5;

    private readonly PubTrackerDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UsersDataAccess(PubTrackerDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<User> GetUserByIdAsync(int userId)
    {
        return await _dbContext.Users
            .Where(u => u.Id == userId)
            .Select(ToUser)
            .FirstOrDefaultAsync();
    }

    public async Task<Paged<User>> GetUsersPageAsync(int page, int pageSize)
    {
        var totalCount = await _dbContext.Users.CountAsync();
        var items = await _dbContext.Users
            .OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToUser)
            .ToListAsync();
        return new Paged<User> { Items = items, TotalCount = totalCount };
    }

    public async Task<List<SearchQuery>> GetSearchQueriesByUserAsync(int userId)
    {
        return await ToSearchQueries(UserSearchQueryEntities(userId)).ToListAsync();
    }

    public async Task<Paged<SearchQuery>> GetSearchQueriesPageByUserAsync(int userId, int page, int pageSize)
    {
        var entities = UserSearchQueryEntities(userId);
        var totalCount = await entities.CountAsync();
        var items = await ToSearchQueries(entities.OrderBy(sq => sq.Id).Skip((page - 1) * pageSize).Take(pageSize))
            .ToListAsync();
        return new Paged<SearchQuery> { Items = items, TotalCount = totalCount };
    }

    private IQueryable<SearchQueryEntity> UserSearchQueryEntities(int userId)
    {
        return _dbContext.UserSearchQueries
            .Where(usq => usq.UserId == userId)
            .Join(_dbContext.SearchQueries, usq => usq.SearchQueryId, sq => sq.Id, (usq, sq) => sq);
    }

    private IQueryable<SearchQuery> ToSearchQueries(IQueryable<SearchQueryEntity> entities)
    {
        return entities.Select(sq => new SearchQuery
        {
            Id = sq.Id,
            SourceId = sq.SourceId ?? 0,
            TargetUrl = sq.TargetUrl ?? string.Empty,
            LastDigestSentAt = sq.LastDigestSentAt,
            StoredRecordCount = _dbContext.SearchQueryRecords.Count(sqr => sqr.SearchQueryId == sq.Id),
            LastFetchedAt = _dbContext.SearchQueryRecords
                .Where(sqr => sqr.SearchQueryId == sq.Id)
                .Max(sqr => (DateTime?)sqr.FirstSeenAt),
            SourceRecordCount = sq.SourceRecordCount,
            LastPollFailedAt = sq.LastPollFailedAt
        });
    }

    public async Task<User> CreateUserAsync(User user)
    {
        var entity = new UserEntity
        {
            Username = user.Username,
            Email = user.Email,
            IsMarkedForDeletion = user.IsMarkedForDeletion,
            DeletionRequestedAt = user.DeletionRequestedAt
        };
        _dbContext.Users.Add(entity);
        await _dbContext.SaveChangesAsync();
        user.Id = entity.Id;
        return user;
    }

    public async Task<User> GetOrProvisionByKeycloakSubAsync(string keycloakSub, string email, string username)
    {
        var existing = await _dbContext.Users
            .Where(u => u.KeycloakSub == keycloakSub)
            .Select(ToUser)
            .FirstOrDefaultAsync();
        if (existing != null)
        {
            await RecordActivityAsync(existing.Id);
            return await ReactivateIfWithinGraceAsync(existing);
        }

        var linkedRows = await _dbContext.Database.SqlQuery<int>(
                $"""
                 UPDATE users SET keycloak_sub = {keycloakSub}
                 WHERE email = {email} AND keycloak_sub IS NULL
                 RETURNING id
                 """)
            .ToListAsync();
        if (linkedRows.Count > 0)
        {
            await RecordActivityAsync(linkedRows[0]);
            return await ReactivateIfWithinGraceAsync(await GetUserByIdAsync(linkedRows[0]));
        }

        var entity = new UserEntity { Username = username, Email = email, KeycloakSub = keycloakSub };
        _dbContext.Users.Add(entity);
        await _dbContext.SaveChangesAsync();
        return new User
        {
            Id = entity.Id,
            Username = entity.Username,
            Email = entity.Email,
            DigestsStopped = entity.DigestsStopped,
            IsMarkedForDeletion = entity.IsMarkedForDeletion,
            DeletionRequestedAt = entity.DeletionRequestedAt
        };
    }

    // Every authenticated request lands here, so the write is throttled to once a day; it also
    // cancels a pending inactivity warning (the user came back).
    private async Task RecordActivityAsync(int userId)
    {
        var now = DateTime.UtcNow;
        var staleBefore = now.AddDays(-1);
        await _dbContext.Users
            .Where(u => u.Id == userId && (u.LastActiveAt < staleBefore || u.InactivityNoticeSentAt != null))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.LastActiveAt, now)
                .SetProperty(u => u.InactivityNoticeSentAt, (DateTime?)null));
    }

    public async Task<List<User>> GetUsersDueInactivityNoticeAsync()
    {
        var cutoff = DateTime.UtcNow.AddYears(-InactivityYears);
        return await _dbContext.Users
            .Where(u => !u.IsMarkedForDeletion && u.InactivityNoticeSentAt == null && u.LastActiveAt < cutoff)
            .OrderBy(u => u.Id)
            .Select(ToUser)
            .ToListAsync();
    }

    public async Task MarkInactivityNoticeSentAsync(int userId)
    {
        var now = DateTime.UtcNow;
        await _dbContext.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.InactivityNoticeSentAt, now));
    }

    // Signing back in within the grace period undoes the soft delete; after it the account stays
    // marked and the purge worker removes it. The boundary matches PurgeExpiredDeletedUsersAsync.
    private async Task<User> ReactivateIfWithinGraceAsync(User user)
    {
        var cutoff = DateTime.UtcNow.AddDays(-DeletionGracePeriodDays);
        if (!user.IsMarkedForDeletion || user.DeletionRequestedAt is not { } requestedAt || requestedAt < cutoff)
        {
            return user;
        }

        await _dbContext.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.IsMarkedForDeletion, false)
                .SetProperty(u => u.DeletionRequestedAt, (DateTime?)null));

        user.IsMarkedForDeletion = false;
        user.DeletionRequestedAt = null;
        return user;
    }

    private static readonly System.Linq.Expressions.Expression<Func<UserEntity, User>> ToUser = u => new User
    {
        Id = u.Id,
        Username = u.Username,
        Email = u.Email,
        DigestsStopped = u.DigestsStopped,
        IsMarkedForDeletion = u.IsMarkedForDeletion,
        DeletionRequestedAt = u.DeletionRequestedAt
    };

    public async Task UpdateUserAsync(int userId, User user)
    {
        await _dbContext.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.Username, user.Username)
                .SetProperty(u => u.Email, user.Email));
    }

    public async Task SetDigestsStoppedAsync(int userId, bool stopped)
    {
        await _dbContext.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.DigestsStopped, stopped));
    }

    public async Task SoftDeleteUserAsync(int userId)
    {
        var deletionTime = DateTime.UtcNow;
        await _dbContext.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.IsMarkedForDeletion, true)
                .SetProperty(u => u.DeletionRequestedAt, deletionTime));
    }

    // The one definition of "due for purge", used to pick users and to re-check under the row lock.
    private static System.Linq.Expressions.Expression<Func<UserEntity, bool>> DueForPurge(DateTime cutoff) =>
        u => (u.IsMarkedForDeletion && u.DeletionRequestedAt < cutoff) || u.InactivityNoticeSentAt < cutoff;

    public async Task<List<int>> GetUserIdsDuePurgeAsync()
    {
        var cutoff = DateTime.UtcNow.AddDays(-DeletionGracePeriodDays);
        return await _dbContext.Users
            .Where(DueForPurge(cutoff))
            .OrderBy(u => u.Id)
            .Select(u => u.Id)
            .ToListAsync();
    }

    public async Task<bool> PurgeUserAsync(int userId, Func<string?, Task> deleteExternalAccount)
    {
        var cutoff = DateTime.UtcNow.AddDays(-DeletionGracePeriodDays);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        await _dbContext.Database.SqlQuery<int>($"""SELECT id AS "Value" FROM users WHERE id = {userId} FOR UPDATE""").ToListAsync();
        var keycloakSub = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Where(DueForPurge(cutoff))
            .Select(u => new { u.KeycloakSub })
            .FirstOrDefaultAsync();
        if (keycloakSub == null)
        {
            return false;
        }

        // If this throws, the transaction is rolled back on dispose and the user stays. If the
        // commit below fails after it succeeded, the next cycle repeats it (a deleted account is
        // a no-op) and finishes the purge.
        await deleteExternalAccount(keycloakSub.KeycloakSub);

        await _dbContext.UserSearchQueryDigests.Where(d => d.UserId == userId).ExecuteDeleteAsync();
        await _dbContext.UserSearchQueries.Where(q => q.UserId == userId).ExecuteDeleteAsync();
        // digest_outbox rows go with the user through their ON DELETE CASCADE.
        await _dbContext.Users.Where(u => u.Id == userId).ExecuteDeleteAsync();

        await transaction.CommitAsync();
        return true;
    }
}
