using RecordData;
using RecordService.Authentication;

namespace RecordService.BusinessLogic.UsersService;

public interface IUsersService
{
    // User-scoped and admin-only methods take the Caller and throw ForbiddenException when it may not act.
    // The rest (CreateUserAsync, provisioning, purge, notices) are system operations with no user caller.
    Task<User> GetUserByIdAsync(Caller caller, int userId);
    Task<Paged<User>> GetUsersPageAsync(Caller caller, int page, int pageSize);
    Task<List<SearchQuery>> GetSearchQueriesByUserAsync(Caller caller, int userId);
    Task<Paged<SearchQuery>> GetSearchQueriesPageByUserAsync(Caller caller, int userId, int page, int pageSize);
    Task<User> CreateUserAsync(User user);
    Task<User> GetOrProvisionByKeycloakSubAsync(string keycloakSub, string email, string username);
    /// <param name="keycloakSub">The caller's Keycloak id. When given and the email changes, the Keycloak account is updated too.</param>
    Task UpdateUserAsync(Caller caller, int userId, User user, string? keycloakSub = null);
    Task SetDigestsStoppedAsync(Caller caller, int userId, bool stopped);
    Task SoftDeleteUserAsync(Caller caller, int userId);
    Task<int> PurgeExpiredDeletedUsersAsync();

    /// <summary>Emails each user inactive for 5 years once, warning that the account is purged in 30 days unless they sign in. Returns how many were sent.</summary>
    Task<int> SendInactivityNoticesAsync();
}
