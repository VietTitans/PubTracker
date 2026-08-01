using RecordData;

namespace RecordService.DataAccess;

public interface IUsersDataAccess
{
    Task<User> GetUserByIdAsync(int userId);
    Task<List<User>> GetUsersAsync();
    Task<List<string>> GetSearchQueriesByUserAsync(int userId);
    Task<User> CreateUserAsync(User user);
    Task UpdateUserAsync(int userId, User user);
    Task SoftDeleteUserAsync(int userId);
}
