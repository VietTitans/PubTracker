using RecordData;
using RecordService.DataAccess;

namespace RecordService.BusinessLogic.UsersService;

public class UsersService : IUsersService
{
    private readonly IUsersDataAccess _dataAccess;
    private readonly ILogger<UsersService> _logger;

    public UsersService(IUsersDataAccess dataAccess, ILogger<UsersService> logger)
    {
        _dataAccess = dataAccess;
        _logger = logger;
    }

    public async Task<User> GetUserByIdAsync(int userId)
    {
        return await _dataAccess.GetUserByIdAsync(userId);
    }

    public async Task<List<User>> GetUsersAsync()
    {
        return await _dataAccess.GetUsersAsync();
    }

    public async Task<List<SearchQuery>> GetSearchQueriesByUserAsync(int userId)
    {
        return await _dataAccess.GetSearchQueriesByUserAsync(userId);
    }

    public async Task<User> CreateUserAsync(User user)
    {
        return await _dataAccess.CreateUserAsync(user);
    }

    public async Task<User> GetOrProvisionByKeycloakSubAsync(string keycloakSub, string email, string name, string username)
    {
        return await _dataAccess.GetOrProvisionByKeycloakSubAsync(keycloakSub, email, name, username);
    }

    public async Task UpdateUserAsync(int userId, User user)
    {
        await _dataAccess.UpdateUserAsync(userId, user);
    }

    public async Task SoftDeleteUserAsync(int userId)
    {
        _logger.LogInformation("User {UserId} requested account deletion", userId);
        try
        {
            await _dataAccess.SoftDeleteUserAsync(userId);
            _logger.LogInformation("User {UserId} marked for deletion", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark user {UserId} for deletion", userId);
            throw;
        }
    }

    public async Task<int> PurgeExpiredDeletedUsersAsync()
    {
        var purgedCount = await _dataAccess.PurgeExpiredDeletedUsersAsync();
        if (purgedCount > 0)
        {
            _logger.LogInformation("Purged {PurgedCount} user(s) past their deletion grace period", purgedCount);
        }
        return purgedCount;
    }

}
