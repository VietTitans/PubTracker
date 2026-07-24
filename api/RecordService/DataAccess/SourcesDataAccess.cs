using Npgsql;
using RecordData;
using System.Diagnostics;

namespace RecordService.DataAccess;

public class SourcesDataAccess
{
    private readonly string _connectionString;

    public SourcesDataAccess(string connectionString)
    {
        _connectionString = connectionString;
        Debug.WriteLine($"Connection string: '{connectionString}'");
    }
    public async Task<List<User>> GetUsersAsync()
    {
        var users = new List<User>();
        using (var connection = new NpgsqlConnection (_connectionString))
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
