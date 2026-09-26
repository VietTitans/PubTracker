using Microsoft.EntityFrameworkCore;
using RecordData;
using RecordService.DataAccess.ExternalSources;
using RecordService.Exceptions;
using RecordService.Models;

namespace RecordService.DataAccess;

public class SearchQueriesDataAccess : ISearchQueriesDataAccess
{
    private readonly PubTrackerDbContext _dbContext;
    private readonly LiteratureSourceFactory _sourceFactory;

    public SearchQueriesDataAccess(PubTrackerDbContext dbContext, LiteratureSourceFactory sourceFactory)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
    }

    public async Task<SearchQuery?> GetSearchQueryByIdAsync(int searchQueryId)
    {
        return await _dbContext.SearchQueries
            .Where(sq => sq.Id == searchQueryId)
            .Select(sq => new SearchQuery
            {
                Id = sq.Id,
                SourceId = sq.SourceId ?? 0,
                TargetUrl = sq.TargetUrl ?? string.Empty,
                LastDigestSentAt = sq.LastDigestSentAt,
                RecordCount = _dbContext.SearchQueryRecords.Count(sqr => sqr.SearchQueryId == sq.Id),
                LastFetchedAt = _dbContext.SearchQueryRecords
                    .Where(sqr => sqr.SearchQueryId == sq.Id)
                    .Max(sqr => (DateTime?)sqr.FirstSeenAt),
                SourceRecordCount = sq.SourceRecordCount,
                LastPolledAt = sq.LastPolledAt
            })
            .FirstOrDefaultAsync();
    }

    public async Task<List<SearchQuery>> GetAllSearchQueriesAsync()
    {
        return await _dbContext.SearchQueries
            .Select(sq => new SearchQuery
            {
                Id = sq.Id,
                SourceId = sq.SourceId ?? 0,
                TargetUrl = sq.TargetUrl ?? string.Empty,
                LastDigestSentAt = sq.LastDigestSentAt,
                LastPolledAt = sq.LastPolledAt
            })
            .ToListAsync();
    }

    public async Task<List<int>> GetUserSubscribersForQueryAsync(int searchQueryId)
    {
        return await _dbContext.UserSearchQueries
            .Where(usq => usq.SearchQueryId == searchQueryId)
            .Select(usq => usq.UserId)
            .ToListAsync();
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
        var baseUrl = new Uri(targetUrl).GetLeftPart(UriPartial.Authority);

        await using (var transaction = await _dbContext.Database.BeginTransactionAsync())
        {
            try
            {
                var sourceId = (await _dbContext.Database.SqlQuery<int>(
                    $"""
                     INSERT INTO sources (name, base_url)
                     VALUES ({provider.ProviderName}, {baseUrl})
                     ON CONFLICT (name) DO UPDATE SET name = EXCLUDED.name
                     RETURNING id
                     """).ToListAsync()).Single();

                var searchQueryId = (await _dbContext.Database.SqlQuery<int>(
                    $"""
                     INSERT INTO search_queries (source_id, target_url)
                     VALUES ({sourceId}, {targetUrl})
                     ON CONFLICT (source_id, target_url)
                     DO UPDATE SET source_id = EXCLUDED.source_id
                     RETURNING id
                     """).ToListAsync()).Single();

                var insertedIds = await _dbContext.Database.SqlQuery<int>(
                    $"""
                     INSERT INTO user_search_queries (user_id, search_query_id)
                     VALUES ({userId}, {searchQueryId})
                     ON CONFLICT (user_id, search_query_id) DO NOTHING
                     RETURNING id
                     """).ToListAsync();
                if (insertedIds.Count == 0)
                {
                    throw new AlreadySubscribedException(
                        $"User {userId} is already subscribed to search query {searchQueryId}.");
                }

                // Seeded to NOW() (not left null) so a brand new subscriber only gets records
                // first-seen after they subscribed, not the query's entire historical backlog.
                // ON CONFLICT DO UPDATE (not DO NOTHING) so re-subscribing after unsubscribing
                // always resets to "just subscribed" rather than resuming a stale watermark.
                await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"""
                     INSERT INTO user_search_query_digests (user_id, search_query_id, last_digest_sent_at)
                     VALUES ({userId}, {searchQueryId}, NOW())
                     ON CONFLICT (user_id, search_query_id) DO UPDATE SET last_digest_sent_at = EXCLUDED.last_digest_sent_at
                     """);

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

    public async Task<bool> UnsubscribeAsync(int userId, int searchQueryId)
    {
        var rowsAffected = await _dbContext.UserSearchQueries
            .Where(usq => usq.UserId == userId && usq.SearchQueryId == searchQueryId)
            .ExecuteDeleteAsync();
        return rowsAffected > 0;
    }

    public async Task<List<(int UserId, DateTime? LastDigestSentAt)>> GetUserDigestWatermarksForQueryAsync(int searchQueryId)
    {
        var rows = await (
            from usq in _dbContext.UserSearchQueries
            where usq.SearchQueryId == searchQueryId
            join d in _dbContext.UserSearchQueryDigests
                on new { usq.UserId, SearchQueryId = searchQueryId } equals new { d.UserId, d.SearchQueryId }
                into digestJoin
            from d in digestJoin.DefaultIfEmpty()
            select new { usq.UserId, d.LastDigestSentAt }
        ).ToListAsync();

        return rows.Select(r => (r.UserId, r.LastDigestSentAt)).ToList();
    }

    public async Task UpdateUserDigestWatermarkAsync(int userId, int searchQueryId, DateTime timestamp)
    {
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO user_search_query_digests (user_id, search_query_id, last_digest_sent_at)
             VALUES ({userId}, {searchQueryId}, {timestamp})
             ON CONFLICT (user_id, search_query_id) DO UPDATE SET last_digest_sent_at = EXCLUDED.last_digest_sent_at
             """);
    }

    public async Task<List<PendingUserDigest>> GetOtherPendingDigestsForUserAsync(int userId, IReadOnlyList<int> excludeSearchQueryIds)
    {
        var excludeArray = excludeSearchQueryIds.ToArray();

        var rows = await (
            from usq in _dbContext.UserSearchQueries
            where usq.UserId == userId && usq.SearchQueryId != null && !excludeArray.Contains(usq.SearchQueryId.Value)
            join sq in _dbContext.SearchQueries on usq.SearchQueryId equals sq.Id
            join sqr in _dbContext.SearchQueryRecords on usq.SearchQueryId equals sqr.SearchQueryId
            join r in _dbContext.Records on sqr.RecordId equals r.Id
            join d in _dbContext.UserSearchQueryDigests
                on new { usq.UserId, SearchQueryId = usq.SearchQueryId.Value } equals new { d.UserId, d.SearchQueryId }
                into digestJoin
            from d in digestJoin.DefaultIfEmpty()
            where sqr.FirstSeenAt > (d.LastDigestSentAt ?? DateTime.MinValue)
            orderby sqr.SearchQueryId, sqr.FirstSeenAt
            select new
            {
                SearchQueryId = sqr.SearchQueryId,
                TargetUrl = sq.TargetUrl ?? string.Empty,
                r.ExternalId,
                r.Doi,
                r.Title,
                r.Description,
                r.SourceUrl,
                sqr.FirstSeenAt
            }
        ).ToListAsync();

        return rows.GroupBy(r => (r.SearchQueryId, r.TargetUrl))
            .Select(g => new PendingUserDigest
            {
                UserId = userId,
                SearchQueryId = g.Key.SearchQueryId,
                TargetUrl = g.Key.TargetUrl,
                Records = g.Select(r => new LiteratureRecord
                {
                    ExternalId = r.ExternalId,
                    Doi = r.Doi,
                    Title = r.Title,
                    Abstract = r.Description,
                    SourceUrl = r.SourceUrl,
                    FirstSeenAt = r.FirstSeenAt ?? default
                }).ToList()
            })
            .ToList();
    }

    public async Task RecordPollCompletedAsync(int searchQueryId, DateTime polledAt, int? sourceRecordCount)
    {
        await _dbContext.SearchQueries
            .Where(sq => sq.Id == searchQueryId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(sq => sq.LastPolledAt, polledAt)
                .SetProperty(sq => sq.SourceRecordCount, sq => sourceRecordCount ?? sq.SourceRecordCount));
    }
}
