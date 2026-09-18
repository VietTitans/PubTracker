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
            using (var command = new NpgsqlCommand(
                @"SELECT sq.id, sq.source_id, sq.target_url, sq.last_digest_sent_at,
                         agg.record_count, agg.last_fetched_at,
                         sq.source_record_count,
                         sq.last_polled_at
                  FROM search_queries sq
                  LEFT JOIN LATERAL (
                      SELECT COUNT(*) AS record_count, MAX(sqr.first_seen_at) AS last_fetched_at
                      FROM search_query_records sqr
                      WHERE sqr.search_query_id = sq.id
                  ) agg ON true
                  WHERE sq.id = @id", connection))
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
                            TargetUrl = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            LastDigestSentAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                            RecordCount = (int)reader.GetInt64(4),
                            LastFetchedAt = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                            SourceRecordCount = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                            LastPolledAt = reader.IsDBNull(7) ? null : reader.GetDateTime(7)
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
            using (var command = new NpgsqlCommand("SELECT id, source_id, target_url, last_digest_sent_at, last_polled_at FROM search_queries", connection))
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
                            LastDigestSentAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                            LastPolledAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
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
                    using (var upsertSourceCommand = new NpgsqlCommand(
                        @"INSERT INTO sources (name, base_url)
                          VALUES (@name, @baseUrl)
                          ON CONFLICT (name) DO UPDATE SET name = EXCLUDED.name
                          RETURNING id", connection, transaction))
                    {
                        var baseUrl = new Uri(targetUrl).GetLeftPart(UriPartial.Authority);
                        upsertSourceCommand.Parameters.AddWithValue("@name", provider.ProviderName);
                        upsertSourceCommand.Parameters.AddWithValue("@baseUrl", baseUrl);
                        sourceId = (int)(await upsertSourceCommand.ExecuteScalarAsync())!;
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

                    // Seeded to NOW() (not left null) so a brand new subscriber only gets records
                    // first-seen after they subscribed, not the query's entire historical backlog.
                    // ON CONFLICT DO UPDATE (not DO NOTHING) so re-subscribing after unsubscribing
                    // always resets to "just subscribed" rather than resuming a stale watermark.
                    using (var seedDigestWatermarkCommand = new NpgsqlCommand(
                        @"INSERT INTO user_search_query_digests (user_id, search_query_id, last_digest_sent_at)
                          VALUES (@userId, @searchQueryId, NOW())
                          ON CONFLICT (user_id, search_query_id) DO UPDATE SET last_digest_sent_at = EXCLUDED.last_digest_sent_at",
                        connection, transaction))
                    {
                        seedDigestWatermarkCommand.Parameters.AddWithValue("@userId", userId);
                        seedDigestWatermarkCommand.Parameters.AddWithValue("@searchQueryId", searchQueryId);
                        await seedDigestWatermarkCommand.ExecuteNonQueryAsync();
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

    public async Task<bool> UnsubscribeAsync(int userId, int searchQueryId)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand(
                "DELETE FROM user_search_queries WHERE user_id = @userId AND search_query_id = @searchQueryId", connection))
            {
                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@searchQueryId", searchQueryId);
                var rowsAffected = await command.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
        }
    }

    public async Task<List<(int UserId, DateTime? LastDigestSentAt)>> GetUserDigestWatermarksForQueryAsync(int searchQueryId)
    {
        var result = new List<(int, DateTime?)>();
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand(
                @"SELECT usq.user_id, uqd.last_digest_sent_at
                  FROM user_search_queries usq
                  LEFT JOIN user_search_query_digests uqd
                      ON uqd.user_id = usq.user_id AND uqd.search_query_id = usq.search_query_id
                  WHERE usq.search_query_id = @searchQueryId", connection))
            {
                command.Parameters.AddWithValue("@searchQueryId", searchQueryId);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.Add((reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetDateTime(1)));
                    }
                }
            }
        }
        return result;
    }

    public async Task UpdateUserDigestWatermarkAsync(int userId, int searchQueryId, DateTime timestamp)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand(
                @"INSERT INTO user_search_query_digests (user_id, search_query_id, last_digest_sent_at)
                  VALUES (@userId, @searchQueryId, @timestamp)
                  ON CONFLICT (user_id, search_query_id) DO UPDATE SET last_digest_sent_at = EXCLUDED.last_digest_sent_at", connection))
            {
                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@searchQueryId", searchQueryId);
                command.Parameters.AddWithValue("@timestamp", timestamp);
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    public async Task<List<PendingUserDigest>> GetOtherPendingDigestsForUserAsync(int userId, IReadOnlyList<int> excludeSearchQueryIds)
    {
        var rows = new List<(int SearchQueryId, string TargetUrl, LiteratureRecord Record)>();
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand(
                @"SELECT sqr.search_query_id, sq.target_url,
                         r.external_id, r.doi, r.title, r.description, r.source_url, sqr.first_seen_at
                  FROM user_search_queries usq
                  JOIN search_queries sq ON sq.id = usq.search_query_id
                  JOIN search_query_records sqr ON sqr.search_query_id = usq.search_query_id
                  JOIN records r ON r.id = sqr.record_id
                  LEFT JOIN user_search_query_digests uqd
                      ON uqd.user_id = usq.user_id AND uqd.search_query_id = usq.search_query_id
                  WHERE usq.user_id = @userId
                    AND usq.search_query_id <> ALL(@excludeSearchQueryIds)
                    AND sqr.first_seen_at > COALESCE(uqd.last_digest_sent_at, '-infinity'::timestamptz)
                  ORDER BY sqr.search_query_id, sqr.first_seen_at", connection))
            {
                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.AddWithValue("@excludeSearchQueryIds", excludeSearchQueryIds.ToArray());
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        rows.Add((reader.GetInt32(0), reader.GetString(1), new LiteratureRecord
                        {
                            ExternalId = reader.GetString(2),
                            Doi = reader.IsDBNull(3) ? null : reader.GetString(3),
                            Title = reader.GetString(4),
                            Abstract = reader.IsDBNull(5) ? null : reader.GetString(5),
                            SourceUrl = reader.IsDBNull(6) ? null : reader.GetString(6),
                            FirstSeenAt = reader.GetDateTime(7)
                        }));
                    }
                }
            }
        }

        return rows.GroupBy(r => (r.SearchQueryId, r.TargetUrl))
            .Select(g => new PendingUserDigest
            {
                UserId = userId,
                SearchQueryId = g.Key.SearchQueryId,
                TargetUrl = g.Key.TargetUrl,
                Records = g.Select(r => r.Record).ToList()
            })
            .ToList();
    }

    public async Task RecordPollCompletedAsync(int searchQueryId, DateTime polledAt, int? sourceRecordCount)
    {
        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand(
                @"UPDATE search_queries
                  SET last_polled_at = @polledAt,
                      source_record_count = COALESCE(@sourceRecordCount, source_record_count)
                  WHERE id = @id", connection))
            {
                command.Parameters.AddWithValue("@polledAt", polledAt);
                command.Parameters.AddWithValue("@sourceRecordCount", (object?)sourceRecordCount ?? DBNull.Value);
                command.Parameters.AddWithValue("@id", searchQueryId);
                await command.ExecuteNonQueryAsync();
            }
        }
    }
}
