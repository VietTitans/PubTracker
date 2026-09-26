using Microsoft.EntityFrameworkCore;
using RecordData;
using RecordService.DataAccess.Entities;

namespace RecordService.DataAccess;

public class UsersDataAccess : IUsersDataAccess
{
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
                SourceRecordCount = sq.SourceRecordCount
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
            return existing;
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
            return await GetUserByIdAsync(linkedRows[0]);
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
}
