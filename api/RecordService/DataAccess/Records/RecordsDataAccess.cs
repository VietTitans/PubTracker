using Microsoft.EntityFrameworkCore;
using RecordService.Models;

namespace RecordService.DataAccess;

public class RecordsDataAccess : IRecordsDataAccess
{
    private readonly PubTrackerDbContext _dbContext;

    public RecordsDataAccess(PubTrackerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    private record RecordUpsertRow(int Id, bool WasInserted);

    public async Task<PersistResult> PersistSearchResultsAsync(int searchQueryId, int sourceId, IReadOnlyList<LiteratureRecord> records)
    {
        var newlyLinkedRecords = new List<LiteratureRecord>();
        var newlyInsertedRecords = new List<LiteratureRecord>();

        await using (var transaction = await _dbContext.Database.BeginTransactionAsync())
        {
            try
            {
                foreach (var record in records)
                {
                    var upsertResult = (await _dbContext.Database.SqlQuery<RecordUpsertRow>(
                        $"""
                         INSERT INTO records (external_id, doi, title, description, source_url)
                         VALUES ({record.ExternalId}, {record.Doi}, {record.Title}, {record.Abstract}, {record.SourceUrl})
                         ON CONFLICT (external_id) DO UPDATE
                             SET doi = EXCLUDED.doi, title = EXCLUDED.title, description = EXCLUDED.description, source_url = EXCLUDED.source_url
                         RETURNING id, (xmax = 0) AS "WasInserted"
                         """).ToListAsync()).Single();
                    var recordId = upsertResult.Id;
                    if (upsertResult.WasInserted)
                    {
                        newlyInsertedRecords.Add(record);
                    }

                    await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                         INSERT INTO source_records (record_id, source_id)
                         VALUES ({recordId}, {sourceId})
                         ON CONFLICT (record_id, source_id) DO NOTHING
                         """);

                    var linkedIds = await _dbContext.Database.SqlQuery<int>(
                        $"""
                         INSERT INTO search_query_records (search_query_id, record_id, first_seen_at)
                         VALUES ({searchQueryId}, {recordId}, NOW())
                         ON CONFLICT (search_query_id, record_id) DO NOTHING
                         RETURNING record_id
                         """).ToListAsync();
                    if (linkedIds.Count > 0)
                    {
                        newlyLinkedRecords.Add(record);
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

        return new PersistResult { NewlyLinkedRecords = newlyLinkedRecords, NewlyInsertedRecords = newlyInsertedRecords };
    }

    public async Task UpdateAuthorIntentionAsync(string externalId, string authorIntention)
    {
        await _dbContext.Records
            .Where(r => r.ExternalId == externalId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.AuthorIntention, authorIntention));
    }

    public async Task<List<LiteratureRecord>> GetRecordsSeenSinceAsync(int searchQueryId, DateTime? since)
    {
        var effectiveSince = DateTime.SpecifyKind(since ?? DateTime.MinValue, DateTimeKind.Utc);

        return await (
            from sqr in _dbContext.SearchQueryRecords
            join r in _dbContext.Records on sqr.RecordId equals r.Id
            where sqr.SearchQueryId == searchQueryId && sqr.FirstSeenAt > effectiveSince
            orderby sqr.FirstSeenAt
            select new LiteratureRecord
            {
                ExternalId = r.ExternalId,
                Doi = r.Doi,
                Title = r.Title,
                Abstract = r.Description,
                SourceUrl = r.SourceUrl,
                AuthorIntention = r.AuthorIntention,
                FirstSeenAt = sqr.FirstSeenAt ?? default
            }
        ).ToListAsync();
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
