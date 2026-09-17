using RecordData;
using RecordService.DataAccess;

namespace RecordService.BusinessLogic.UsersService;

public class UsersService : IUsersService
{
    private readonly IUsersDataAccess _dataAccess;

    public UsersService(IUsersDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
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
        await _dataAccess.SoftDeleteUserAsync(userId);
    }

}
