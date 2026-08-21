using Npgsql;
using RecordData;

namespace RecordService.DataAccess;

public class UsersDataAccess : IUsersDataAccess
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
            using (var command = new NpgsqlCommand("SELECT id, name, username, email, is_marked_for_deletion, deletion_requested_at FROM users WHERE id = @id", connection))
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
                            Email = reader.GetString(3),
                            IsMarkedForDeletion = reader.GetBoolean(4),
                            DeletionRequestedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
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
            using (var command = new NpgsqlCommand("SELECT id, name, username, email, is_marked_for_deletion, deletion_requested_at FROM users", connection))
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
                            Email = reader.GetString(3),
                            IsMarkedForDeletion = reader.GetBoolean(4),
                            DeletionRequestedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
                        };
                        users.Add(user);
                    }
                }
            }
        }
        return users;
    }

    public async Task<List<string>> GetSearchQueriesByUserAsync(int userId)
    {
        var queries = new List<string>();
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand("SELECT sq.id FROM search_queries sq INNER JOIN user_search_queries usq ON sq.id = usq.search_query_id WHERE usq.user_id = @userId", connection))
            {
                command.Parameters.AddWithValue("@userId", userId);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        queries.Add(reader.GetInt32(0).ToString());
                    }
                }
            }
        }
        return queries;
    }

    public async Task<User> CreateUserAsync(User user)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand("INSERT INTO users (name, username, email, is_marked_for_deletion, deletion_requested_at) VALUES (@name, @username, @email, @isMarkedForDeletion, @deletionRequestedAt) RETURNING id", connection))
            {
                command.Parameters.AddWithValue("@name", user.Name);
                command.Parameters.AddWithValue("@username", user.Username);
                command.Parameters.AddWithValue("@email", user.Email);
                command.Parameters.AddWithValue("@isMarkedForDeletion", user.IsMarkedForDeletion);
                command.Parameters.AddWithValue("@deletionRequestedAt", user.DeletionRequestedAt is not null ? (object)user.DeletionRequestedAt : DBNull.Value);

                var result = await command.ExecuteScalarAsync();
                if (result != null)
                {
                    user.Id = (int)result;
                    return user;
                }

                return null;
            }
        }
    }

    public async Task UpdateUserAsync(int userId, User user)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand("UPDATE users SET name = @name, username = @username, email = @email WHERE id = @id", connection))
            {
                command.Parameters.AddWithValue("@id", userId);
                command.Parameters.AddWithValue("@name", user.Name);
                command.Parameters.AddWithValue("@username", user.Username);
                command.Parameters.AddWithValue("@email", user.Email);
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task SoftDeleteUserAsync(int userId)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand("UPDATE users SET is_marked_for_deletion = true, deletion_requested_at = @deletionTime WHERE id = @id", connection))
            {
                command.Parameters.AddWithValue("@id", userId);
                command.Parameters.AddWithValue("@deletionTime", DateTime.UtcNow);
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}