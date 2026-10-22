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
    /// Ids of users to purge: soft-delete grace period expired, or warned of inactivity more than that
    /// long ago without signing in (see UsersDataAccess.DeletionGracePeriodDays).
    /// </summary>
    Task<List<int>> GetUserIdsDuePurgeAsync();

    /// <summary>
    /// Purges one user in a single transaction: locks the row, re-checks that the user is still due
    /// (one who signed back in meanwhile is kept), runs <paramref name="deleteExternalAccount"/> with
    /// the user's Keycloak sub (null if none), then deletes their subscriptions, watermarks, outbox
    /// rows and the user row. If the callback throws, nothing is deleted. A sign-in for this user
    /// waits on the row lock, so it cannot slip in between the external delete and the row delete.
    /// </summary>
    /// <returns>False if the user was not (or no longer) due.</returns>
    Task<bool> PurgeUserAsync(int userId, Func<string?, Task> deleteExternalAccount);
}
