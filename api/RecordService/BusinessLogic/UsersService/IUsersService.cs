using RecordData;

namespace RecordService.BusinessLogic.UsersService;

public interface IUsersService
{
    Task<User> GetUserByIdAsync(int userId);
    Task<List<User>> GetUsersAsync();
    Task<List<SearchQuery>> GetSearchQueriesByUserAsync(int userId);
    Task<User> CreateUserAsync(User user);
    Task UpdateUserAsync(int userId, User user);
    Task SoftDeleteUserAsync(int userId);
}
