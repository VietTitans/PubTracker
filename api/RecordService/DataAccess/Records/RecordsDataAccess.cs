using Npgsql;
using RecordService.Models;

namespace RecordService.DataAccess;

public class RecordsDataAccess : IRecordsDataAccess
{
    private readonly string _connectionString;

    public RecordsDataAccess(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<List<LiteratureRecord>> PersistSearchResultsAsync(int searchQueryId, int sourceId, IReadOnlyList<LiteratureRecord> records)
    {
        var newlyLinkedRecords = new List<LiteratureRecord>();

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var transaction = await connection.BeginTransactionAsync())
            {
                try
                {
                    foreach (var record in records)
                    {
                        int recordId;
                        using (var upsertRecordCommand = new NpgsqlCommand(
                            @"INSERT INTO records (external_id, doi, title, description, source_url)
                              VALUES (@externalId, @doi, @title, @description, @sourceUrl)
                              ON CONFLICT (external_id) DO UPDATE
                                  SET doi = EXCLUDED.doi, title = EXCLUDED.title, description = EXCLUDED.description, source_url = EXCLUDED.source_url
                              RETURNING id", connection, transaction))
                        {
                            upsertRecordCommand.Parameters.AddWithValue("@externalId", record.ExternalId);
                            upsertRecordCommand.Parameters.AddWithValue("@doi", (object?)record.Doi ?? DBNull.Value);
                            upsertRecordCommand.Parameters.AddWithValue("@title", record.Title);
                            upsertRecordCommand.Parameters.AddWithValue("@description", (object?)record.Abstract ?? DBNull.Value);
                            upsertRecordCommand.Parameters.AddWithValue("@sourceUrl", (object?)record.SourceUrl ?? DBNull.Value);
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

        using (var connection = new NpgsqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new NpgsqlCommand(
                @"SELECT r.external_id, r.doi, r.title, r.description, r.source_url
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
                            SourceUrl = reader.IsDBNull(4) ? null : reader.GetString(4)
                        });
                    }
                }
            }
        }

        return records;
    }
}
