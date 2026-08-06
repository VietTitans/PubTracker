using Npgsql;
using RecordData;

namespace RecordService.DataAccess;

public class SourcesDataAccess : ISourcesDataAccess
{
    private readonly string _connectionString;

    public SourcesDataAccess(string connectionString)
    {
        _connectionString = connectionString; 
    }

    public async Task<List<Source>> GetAllSourcesAsync()
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand("SELECT id, name, base_url FROM sources", connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    var sources = new List<Source>();
                    while (await reader.ReadAsync())
                    {
                        var source = new Source
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            BaseUrl = reader.GetString(2)
                        };
                        sources.Add(source);
                    }
                    return sources;
                }
            }
        }
    }

    public async Task<Source?> GetSourceByIdAsync(int sourceId)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand("SELECT id, name, base_url FROM sources WHERE id = @id", connection))
            {
                command.Parameters.AddWithValue("@id", sourceId);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new Source
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            BaseUrl = reader.GetString(2)
                        };
                    }
                    return null;
                }
            }
        }
    }

}
