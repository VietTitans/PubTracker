using RecordData;
using RecordService.DataAccess;

namespace RecordService.BusinessLogic;

public class UsersService
{
    private readonly UsersDataAccess _dataAccess;

    public UsersService(UsersDataAccess dataAccess)
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

}
