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

    /// <summary>Users inactive for 5 years who have not been warned yet (and are not already being deleted).</summary>
    Task<List<User>> GetUsersDueInactivityNoticeAsync();
    Task MarkInactivityNoticeSentAsync(int userId);

    /// <summary>
    /// Users to purge: soft-delete grace period expired, or warned of inactivity more than that long
    /// ago without signing in (see UsersDataAccess.DeletionGracePeriodDays).
    /// </summary>
    Task<List<(int Id, string? KeycloakSub)>> GetUsersDuePurgeAsync();

    /// <summary>
    /// Hard-deletes the given users (still re-checked against the purge rule, so one who signed back
    /// in meanwhile is kept) along with their subscriptions, watermarks and outbox rows.
    /// Returns the number of users purged.
    /// </summary>
    Task<int> PurgeUsersAsync(IReadOnlyList<int> userIds);
}
