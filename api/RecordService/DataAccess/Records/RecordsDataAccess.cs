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
                            @"INSERT INTO records (doi, title, description)
                              VALUES (@doi, @title, @description)
                              ON CONFLICT (doi) DO UPDATE
                                  SET title = EXCLUDED.title, description = EXCLUDED.description
                              RETURNING id", connection, transaction))
                        {
                            upsertRecordCommand.Parameters.AddWithValue("@doi", record.Doi);
                            upsertRecordCommand.Parameters.AddWithValue("@title", record.Title);
                            upsertRecordCommand.Parameters.AddWithValue("@description", (object?)record.Abstract ?? DBNull.Value);
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
}
