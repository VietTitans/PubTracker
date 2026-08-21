using Npgsql;
using RecordData;
using RecordService.DataAccess.ExternalSources;
using RecordService.Exceptions;
using RecordService.Models;

namespace RecordService.DataAccess;

public class SearchQueriesDataAccess : ISearchQueriesDataAccess
{
    private readonly string _connectionString;
    private readonly LiteratureSourceFactory _sourceFactory;

    public SearchQueriesDataAccess(string connectionString, LiteratureSourceFactory sourceFactory)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
    }

    public async Task<SearchQuery?> GetSearchQueryByIdAsync(int searchQueryId)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand("SELECT id, source_id, target_url FROM search_queries WHERE id = @id", connection))
            {
                command.Parameters.AddWithValue("@id", searchQueryId);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return new SearchQuery
                        {
                            Id = reader.GetInt32(0),
                            SourceId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                            TargetUrl = reader.IsDBNull(2) ? string.Empty : reader.GetString(2)
                        };
                    }

                    return null;
                }
            }
        }
    }

    public async Task<List<SearchQuery>> GetAllSearchQueriesAsync()
    {
        var searchQueries = new List<SearchQuery>();
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand("SELECT id, source_id, target_url, last_digest_sent_at FROM search_queries", connection))
            {
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        searchQueries.Add(new SearchQuery
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
        return searchQueries;
    }

    public async Task<List<int>> GetUserSubscribersForQueryAsync(int searchQueryId)
    {
        var userIds = new List<int>();
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand("SELECT user_id FROM user_search_queries WHERE search_query_id = @searchQueryId", connection))
            {
                command.Parameters.AddWithValue("@searchQueryId", searchQueryId);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        userIds.Add(reader.GetInt32(0));
                    }
                }
            }
        }
        return userIds;
    }

    public async Task<SourceSearchResult> ExecuteSourceSearchAsync(int searchQueryId, string sourceUrl, DateTime? lastRunDate = null)
    {
        try
        {
            // Get the appropriate provider based on URL
            var provider = _sourceFactory.CreateProvider(sourceUrl);

            // Execute the search with the provider
            var result = await provider.SearchAsync(sourceUrl, lastRunDate);

            return result;
        }
        catch (InvalidOperationException ex)
        {
            // No provider found for the URL
            return new SourceSearchResult
            {
                IsSuccessful = false,
                ErrorMessage = ex.Message,
                NewRecordCount = 0,
                NewRecords = new()
            };
        }
        catch (Exception ex)
        {
            // Unexpected error
            return new SourceSearchResult
            {
                IsSuccessful = false,
                ErrorMessage = $"Unexpected error executing search: {ex.Message}",
                NewRecordCount = 0,
                NewRecords = new()
            };
        }
    }

    public async Task<SearchQuery> SubscribeAsync(int userId, string targetUrl)
    {
        // Throws InvalidOperationException if no provider recognizes the URL - this is
        // the "unsupported source" signal the controller maps to a 400 response.
        var provider = _sourceFactory.CreateProvider(targetUrl);

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var transaction = await connection.BeginTransactionAsync())
            {
                try
                {
                    int sourceId;
                    using (var findSourceCommand = new NpgsqlCommand("SELECT id FROM sources WHERE name = @name", connection, transaction))
                    {
                        findSourceCommand.Parameters.AddWithValue("@name", provider.ProviderName);
                        var existingSourceId = await findSourceCommand.ExecuteScalarAsync();
                        if (existingSourceId != null)
                        {
                            sourceId = (int)existingSourceId;
                        }
                        else
                        {
                            var baseUrl = new Uri(targetUrl).GetLeftPart(UriPartial.Authority);
                            using (var insertSourceCommand = new NpgsqlCommand(
                                "INSERT INTO sources (name, base_url) VALUES (@name, @baseUrl) RETURNING id", connection, transaction))
                            {
                                insertSourceCommand.Parameters.AddWithValue("@name", provider.ProviderName);
                                insertSourceCommand.Parameters.AddWithValue("@baseUrl", baseUrl);
                                sourceId = (int)(await insertSourceCommand.ExecuteScalarAsync())!;
                            }
                        }
                    }

                    int searchQueryId;
                    using (var upsertQueryCommand = new NpgsqlCommand(
                        @"INSERT INTO search_queries (source_id, target_url)
                          VALUES (@sourceId, @targetUrl)
                          ON CONFLICT (source_id, target_url)
                          DO UPDATE SET source_id = EXCLUDED.source_id
                          RETURNING id", connection, transaction))
                    {
                        upsertQueryCommand.Parameters.AddWithValue("@sourceId", sourceId);
                        upsertQueryCommand.Parameters.AddWithValue("@targetUrl", targetUrl);
                        searchQueryId = (int)(await upsertQueryCommand.ExecuteScalarAsync())!;
                    }

                    using (var subscribeCommand = new NpgsqlCommand(
                        @"INSERT INTO user_search_queries (user_id, search_query_id)
                          VALUES (@userId, @searchQueryId)
                          ON CONFLICT (user_id, search_query_id) DO NOTHING
                          RETURNING id", connection, transaction))
                    {
                        subscribeCommand.Parameters.AddWithValue("@userId", userId);
                        subscribeCommand.Parameters.AddWithValue("@searchQueryId", searchQueryId);
                        var insertedId = await subscribeCommand.ExecuteScalarAsync();
                        if (insertedId == null)
                        {
                            throw new AlreadySubscribedException(
                                $"User {userId} is already subscribed to search query {searchQueryId}.");
                        }
                    }

                    await transaction.CommitAsync();

                    return new SearchQuery
                    {
                        Id = searchQueryId,
                        SourceId = sourceId,
                        TargetUrl = targetUrl
                    };
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }
    }

    public async Task UpdateLastDigestSentAtAsync(int searchQueryId, DateTime timestamp)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand(
                "UPDATE search_queries SET last_digest_sent_at = @timestamp WHERE id = @id", connection))
            {
                command.Parameters.AddWithValue("@timestamp", timestamp);
                command.Parameters.AddWithValue("@id", searchQueryId);
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}
