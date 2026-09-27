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
}
