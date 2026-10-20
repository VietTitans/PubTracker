using RecordData;

namespace RecordService.DataAccess;

public interface IUsersDataAccess
{
    Task<User> GetUserByIdAsync(int userId);
    Task<Paged<User>> GetUsersPageAsync(int page, int pageSize);
    Task<List<SearchQuery>> GetSearchQueriesByUserAsync(int userId);
    Task<Paged<SearchQuery>> GetSearchQueriesPageByUserAsync(int userId, int page, int pageSize);
    Task<User> CreateUserAsync(User user);

    /// <summary>
    /// Resolves the internal user row for a Keycloak-authenticated principal: matches by
    /// keycloak_sub first, then falls back to linking an existing row by email (so a
    /// pre-existing/seeded user isn't duplicated the first time they log in via Keycloak),
    /// and only creates a new row if neither match is found.
    /// </summary>
    Task<User> GetOrProvisionByKeycloakSubAsync(string keycloakSub, string email, string name, string username);
    Task UpdateUserAsync(int userId, User user);
    Task SetDigestsStoppedAsync(int userId, bool stopped);
    Task SoftDeleteUserAsync(int userId);

    /// <summary>
    /// Hard-deletes users whose soft-delete grace period has expired (see
    /// UsersDataAccess.DeletionGracePeriodDays), along with their search query subscriptions.
    /// Returns the number of users purged.
    /// </summary>
    Task<int> PurgeExpiredDeletedUsersAsync();
}
