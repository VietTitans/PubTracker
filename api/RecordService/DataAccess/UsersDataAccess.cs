using Npgsql;
using RecordData;

namespace RecordService.DataAccess;

public class UsersDataAccess
{
    private readonly string _connectionString;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UsersDataAccess(string connectionString, IHttpContextAccessor httpContextAccessor)
    {
        _connectionString = connectionString;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<User> GetUserByIdAsync(int userId)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand("SELECT id, name, username, email FROM users WHERE id = @id", connection))
            {
                command.Parameters.AddWithValue("@id", userId);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new User
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Username = reader.GetString(2),
                            Email = reader.GetString(3)
                        };
                    }

                    return null;
                }
            }
        }
    }

    public async Task<List<User>> GetUsersAsync()
    {
        var users = new List<User>();
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand("SELECT id, name, username, email FROM users", connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var user = new User
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            Username = reader.GetString(2),
                            Email = reader.GetString(3)
                        };
                        users.Add(user);
                    }
                }
            }
        }
        return users;
    }
}