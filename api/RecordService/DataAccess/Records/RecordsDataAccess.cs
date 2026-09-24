using Npgsql;
using Pgvector;
using RecordService.DataAccess.Embeddings;
using RecordService.Models;

namespace RecordService.DataAccess;

public class RecordsDataAccess : IRecordsDataAccess
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IEmbeddingClient _embeddingClient;

    public RecordsDataAccess(NpgsqlDataSource dataSource, IEmbeddingClient embeddingClient)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _embeddingClient = embeddingClient ?? throw new ArgumentNullException(nameof(embeddingClient));
    }

    public async Task<List<LiteratureRecord>> PersistSearchResultsAsync(int searchQueryId, int sourceId, IReadOnlyList<LiteratureRecord> records)
    {
        var newlyLinkedRecords = new List<LiteratureRecord>();

        using (var connection = await _dataSource.OpenConnectionAsync())
        {
            // Every poll re-fetches a search query's whole result set, not just what's new, so
            // most records here are already embedded from a prior poll. Skip those rather than
            // paying an OpenAI call for them again - the only ones that need embedding are
            // records this batch has never seen before.
            var alreadyEmbedded = await GetExternalIdsWithEmbeddingAsync(connection, records.Select(r => r.ExternalId).ToList());
            var toEmbed = records.Where(r => !alreadyEmbedded.Contains(r.ExternalId)).ToList();

            // Embedded up front, outside the transaction: one batched call instead of one
            // sequential API round-trip per record inside an open transaction, which would hold
            // row locks for the duration of a large first subscribe.
            var newEmbeddings = await _embeddingClient.EmbedBatchAsync(toEmbed.Select(BuildEmbeddingText).ToList());
            var embeddingsByExternalId = toEmbed
                .Zip(newEmbeddings, (record, embedding) => (record.ExternalId, embedding))
                .ToDictionary(x => x.ExternalId, x => x.embedding);

            using (var transaction = await connection.BeginTransactionAsync())
            {
                try
                {
                    for (var i = 0; i < records.Count; i++)
                    {
                        var record = records[i];
                        // Null for an already-embedded record too - COALESCE below then keeps
                        // its existing embedding rather than clobbering it.
                        var embedding = embeddingsByExternalId.GetValueOrDefault(record.ExternalId);

                        int recordId;
                        using (var upsertRecordCommand = new NpgsqlCommand(
                            @"INSERT INTO records (external_id, doi, title, description, source_url, embedding)
                              VALUES (@externalId, @doi, @title, @description, @sourceUrl, @embedding)
                              ON CONFLICT (external_id) DO UPDATE
                                  SET doi = EXCLUDED.doi, title = EXCLUDED.title, description = EXCLUDED.description, source_url = EXCLUDED.source_url,
                                      embedding = COALESCE(EXCLUDED.embedding, records.embedding)
                              RETURNING id", connection, transaction))
                        {
                            upsertRecordCommand.Parameters.AddWithValue("@externalId", record.ExternalId);
                            upsertRecordCommand.Parameters.AddWithValue("@doi", (object?)record.Doi ?? DBNull.Value);
                            upsertRecordCommand.Parameters.AddWithValue("@title", record.Title);
                            upsertRecordCommand.Parameters.AddWithValue("@description", (object?)record.Abstract ?? DBNull.Value);
                            upsertRecordCommand.Parameters.AddWithValue("@sourceUrl", (object?)record.SourceUrl ?? DBNull.Value);
                            upsertRecordCommand.Parameters.Add(new NpgsqlParameter
                            {
                                ParameterName = "@embedding",
                                DataTypeName = "vector",
                                Value = embedding is null ? DBNull.Value : new Vector(embedding)
                            });
                            recordId = (int)(await upsertRecordCommand.ExecuteScalarAsync())!;
                        }

                        using (var linkSourceCommand = new NpgsqlCommand(
                            @"INSERT INTO source_records (record_id, source_id)
                              VALUES (@recordId, @sourceId)
                              ON CONFLICT (record_id, source_id) DO NOTHING", connection, transaction))
                        {
                            linkSourceCommand.Parameters.AddWithValue("@recordId", recordId);
                            linkSourceCommand.Parameters.AddWithValue("@sourceId", sourceId);
                            await linkSourceCommand.ExecuteNonQueryAsync();
                        }

                        using (var linkQueryCommand = new NpgsqlCommand(
                            @"INSERT INTO search_query_records (search_query_id, record_id, first_seen_at)
                              VALUES (@searchQueryId, @recordId, NOW())
                              ON CONFLICT (search_query_id, record_id) DO NOTHING
                              RETURNING record_id", connection, transaction))
                        {
                            linkQueryCommand.Parameters.AddWithValue("@searchQueryId", searchQueryId);
                            linkQueryCommand.Parameters.AddWithValue("@recordId", recordId);
                            var insertedRecordId = await linkQueryCommand.ExecuteScalarAsync();
                            if (insertedRecordId != null)
                            {
                                newlyLinkedRecords.Add(record);
                            }
                        }
                    }

                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        return newlyLinkedRecords;
    }

    public async Task<List<LiteratureRecord>> GetRecordsSeenSinceAsync(int searchQueryId, DateTime? since)
    {
        var records = new List<LiteratureRecord>();
        var effectiveSince = DateTime.SpecifyKind(since ?? DateTime.MinValue, DateTimeKind.Utc);

        using (var connection = await _dataSource.OpenConnectionAsync())
        {
            using (var command = new NpgsqlCommand(
                @"SELECT r.external_id, r.doi, r.title, r.description, r.source_url, sqr.first_seen_at
                  FROM search_query_records sqr
                  JOIN records r ON r.id = sqr.record_id
                  WHERE sqr.search_query_id = @searchQueryId AND sqr.first_seen_at > @since
                  ORDER BY sqr.first_seen_at", connection))
            {
                command.Parameters.AddWithValue("@searchQueryId", searchQueryId);
                command.Parameters.AddWithValue("@since", effectiveSince);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        records.Add(new LiteratureRecord
                        {
                            ExternalId = reader.GetString(0),
                            Doi = reader.IsDBNull(1) ? null : reader.GetString(1),
                            Title = reader.GetString(2),
                            Abstract = reader.IsDBNull(3) ? null : reader.GetString(3),
                            SourceUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
                            FirstSeenAt = reader.GetDateTime(5)
                        });
                    }
                }
            }
        }

        return records;
    }

    public async Task<List<LiteratureRecord>> SearchSimilarRecordsAsync(int userId, float[] queryEmbedding, int topK)
    {
        var records = new List<LiteratureRecord>();

        using (var connection = await _dataSource.OpenConnectionAsync())
        {
            using (var command = new NpgsqlCommand(
                @"WITH user_records AS (
                      SELECT DISTINCT sqr.record_id
                      FROM user_search_queries usq
                      JOIN search_query_records sqr ON sqr.search_query_id = usq.search_query_id
                      WHERE usq.user_id = @userId
                  )
                  SELECT r.external_id, r.doi, r.title, r.description, r.source_url
                  FROM records r
                  JOIN user_records ur ON ur.record_id = r.id
                  WHERE r.embedding IS NOT NULL
                  ORDER BY r.embedding <=> @queryEmbedding
                  LIMIT @topK", connection))
            {
                command.Parameters.AddWithValue("@userId", userId);
                command.Parameters.Add(new NpgsqlParameter
                {
                    ParameterName = "@queryEmbedding",
                    DataTypeName = "vector",
                    Value = new Vector(queryEmbedding)
                });
                command.Parameters.AddWithValue("@topK", topK);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        records.Add(new LiteratureRecord
                        {
                            ExternalId = reader.GetString(0),
                            Doi = reader.IsDBNull(1) ? null : reader.GetString(1),
                            Title = reader.GetString(2),
                            Abstract = reader.IsDBNull(3) ? null : reader.GetString(3),
                            SourceUrl = reader.IsDBNull(4) ? null : reader.GetString(4)
                        });
                    }
                }
            }
        }

        return records;
    }

    private static string BuildEmbeddingText(LiteratureRecord record)
    {
        return string.IsNullOrWhiteSpace(record.Abstract) ? record.Title : $"{record.Title}\n\n{record.Abstract}";
    }

    private static async Task<HashSet<string>> GetExternalIdsWithEmbeddingAsync(NpgsqlConnection connection, IReadOnlyList<string> externalIds)
    {
        var found = new HashSet<string>();

        using (var command = new NpgsqlCommand(
            "SELECT external_id FROM records WHERE external_id = ANY(@externalIds) AND embedding IS NOT NULL", connection))
        {
            command.Parameters.AddWithValue("@externalIds", externalIds.ToArray());
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    found.Add(reader.GetString(0));
                }
            }
        }

        return found;
    }
}
