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

    public async Task<List<SearchQuery>> GetSearchQueriesByUserAsync(int userId)
    {
        var queries = new List<SearchQuery>();
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand(
                @"SELECT sq.id, sq.source_id, sq.target_url, sq.last_digest_sent_at
                  FROM search_queries sq
                  INNER JOIN user_search_queries usq ON sq.id = usq.search_query_id
                  WHERE usq.user_id = @userId", connection))
            {
                command.Parameters.AddWithValue("@userId", userId);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        queries.Add(new SearchQuery
                        {
                            Id = reader.GetInt32(0),
                            SourceId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                            TargetUrl = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            LastDigestSentAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3)
                        });
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

    public async Task<User> GetOrProvisionByKeycloakSubAsync(string keycloakSub, string email, string name, string username)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            using (var command = new NpgsqlCommand(
                "SELECT id, name, username, email, is_marked_for_deletion, deletion_requested_at FROM users WHERE keycloak_sub = @keycloakSub", connection))
            {
                command.Parameters.AddWithValue("@keycloakSub", keycloakSub);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return ReadUser(reader);
                    }
                }
            }

            using (var command = new NpgsqlCommand(
                @"UPDATE users SET keycloak_sub = @keycloakSub
                  WHERE email = @email AND keycloak_sub IS NULL
                  RETURNING id, name, username, email, is_marked_for_deletion, deletion_requested_at", connection))
            {
                command.Parameters.AddWithValue("@keycloakSub", keycloakSub);
                command.Parameters.AddWithValue("@email", email);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return ReadUser(reader);
                    }
                }
            }

            using (var command = new NpgsqlCommand(
                @"INSERT INTO users (name, username, email, keycloak_sub)
                  VALUES (@name, @username, @email, @keycloakSub)
                  RETURNING id, name, username, email, is_marked_for_deletion, deletion_requested_at", connection))
            {
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue("@username", username);
                command.Parameters.AddWithValue("@email", email);
                command.Parameters.AddWithValue("@keycloakSub", keycloakSub);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    await reader.ReadAsync();
                    return ReadUser(reader);
                }
            }
        }
    }

    private static User ReadUser(NpgsqlDataReader reader)
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