using RecordData;

namespace RecordService.BusinessLogic.UsersService;

public interface IUsersService
{
    Task<User> GetUserByIdAsync(int userId);
    Task<List<User>> GetUsersAsync();
    Task<List<SearchQuery>> GetSearchQueriesByUserAsync(int userId);
    Task<User> CreateUserAsync(User user);
    Task<User> GetOrProvisionByKeycloakSubAsync(string keycloakSub, string email, string name, string username);
    /// <param name="keycloakSub">The caller's Keycloak id. When given and the email changes, the Keycloak account is updated too.</param>
    Task UpdateUserAsync(int userId, User user, string? keycloakSub = null);
    Task SoftDeleteUserAsync(int userId);
    Task<int> PurgeExpiredDeletedUsersAsync();
}
