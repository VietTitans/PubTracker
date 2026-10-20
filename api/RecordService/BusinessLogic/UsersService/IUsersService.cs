using RecordData;

namespace RecordService.BusinessLogic.UsersService;

public interface IUsersService
{
    Task<User> GetUserByIdAsync(int userId);
    Task<Paged<User>> GetUsersPageAsync(int page, int pageSize);
    Task<List<SearchQuery>> GetSearchQueriesByUserAsync(int userId);
    Task<Paged<SearchQuery>> GetSearchQueriesPageByUserAsync(int userId, int page, int pageSize);
    Task<User> CreateUserAsync(User user);
    Task<User> GetOrProvisionByKeycloakSubAsync(string keycloakSub, string email, string name, string username);
    /// <param name="keycloakSub">The caller's Keycloak id. When given and the email changes, the Keycloak account is updated too.</param>
    Task UpdateUserAsync(int userId, User user, string? keycloakSub = null);
    Task SoftDeleteUserAsync(int userId);
    Task<int> PurgeExpiredDeletedUsersAsync();

    /// <summary>Emails each user inactive for 5 years once, warning that the account is purged in 30 days unless they sign in. Returns how many were sent.</summary>
    Task<int> SendInactivityNoticesAsync();
}
