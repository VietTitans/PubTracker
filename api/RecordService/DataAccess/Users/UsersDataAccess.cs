using Microsoft.EntityFrameworkCore;
using RecordData;
using RecordService.DataAccess.Entities;

namespace RecordService.DataAccess;

public class UsersDataAccess : IUsersDataAccess
{
    // How long a soft-deleted account can be revived by signing back in before the purge
    // worker (see UserPurgeBackgroundService) hard-deletes it for good.
    private const int DeletionGracePeriodDays = 14;

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

    public async Task<List<User>> GetUsersAsync()
    {
        return await _dbContext.Users.Select(ToUser).ToListAsync();
    }

    public async Task<List<SearchQuery>> GetSearchQueriesByUserAsync(int userId)
    {
        return await _dbContext.UserSearchQueries
            .Where(usq => usq.UserId == userId)
            .Join(_dbContext.SearchQueries, usq => usq.SearchQueryId, sq => sq.Id, (usq, sq) => sq)
            .Select(sq => new SearchQuery
            {
                Id = sq.Id,
                SourceId = sq.SourceId ?? 0,
                TargetUrl = sq.TargetUrl ?? string.Empty,
                LastDigestSentAt = sq.LastDigestSentAt,
                RecordCount = _dbContext.SearchQueryRecords.Count(sqr => sqr.SearchQueryId == sq.Id),
                LastFetchedAt = _dbContext.SearchQueryRecords
                    .Where(sqr => sqr.SearchQueryId == sq.Id)
                    .Max(sqr => (DateTime?)sqr.FirstSeenAt),
                SourceRecordCount = sq.SourceRecordCount,
                LastPollFailedAt = sq.LastPollFailedAt
            })
            .ToListAsync();
    }

    public async Task<User> CreateUserAsync(User user)
    {
        var entity = new UserEntity
        {
            Name = user.Name,
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

    public async Task<User> GetOrProvisionByKeycloakSubAsync(string keycloakSub, string email, string name, string username)
    {
        var existing = await _dbContext.Users
            .Where(u => u.KeycloakSub == keycloakSub)
            .Select(ToUser)
            .FirstOrDefaultAsync();
        if (existing != null)
        {
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
            return await ReactivateIfWithinGraceAsync(await GetUserByIdAsync(linkedRows[0]));
        }

        var entity = new UserEntity { Name = name, Username = username, Email = email, KeycloakSub = keycloakSub };
        _dbContext.Users.Add(entity);
        await _dbContext.SaveChangesAsync();
        return new User
        {
            Id = entity.Id,
            Name = entity.Name,
            Username = entity.Username,
            Email = entity.Email,
            IsMarkedForDeletion = entity.IsMarkedForDeletion,
            DeletionRequestedAt = entity.DeletionRequestedAt
        };
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
        Name = u.Name,
        Username = u.Username,
        Email = u.Email,
        IsMarkedForDeletion = u.IsMarkedForDeletion,
        DeletionRequestedAt = u.DeletionRequestedAt
    };

    public async Task UpdateUserAsync(int userId, User user)
    {
        await _dbContext.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.Name, user.Name)
                .SetProperty(u => u.Username, user.Username)
                .SetProperty(u => u.Email, user.Email));
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

    public async Task<int> PurgeExpiredDeletedUsersAsync()
    {
        var cutoff = DateTime.UtcNow.AddDays(-DeletionGracePeriodDays);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             DELETE FROM user_search_query_digests
             WHERE user_id IN (
                 SELECT id FROM users WHERE is_marked_for_deletion = true AND deletion_requested_at < {cutoff}
             )
             """);

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             DELETE FROM user_search_queries
             WHERE user_id IN (
                 SELECT id FROM users WHERE is_marked_for_deletion = true AND deletion_requested_at < {cutoff}
             )
             """);

        var purgedCount = await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             DELETE FROM users
             WHERE is_marked_for_deletion = true AND deletion_requested_at < {cutoff}
             """);

        await transaction.CommitAsync();
        return purgedCount;
    }
}
