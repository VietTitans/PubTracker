using Microsoft.EntityFrameworkCore;
using RecordData;
using RecordService.DataAccess.Entities;

namespace RecordService.DataAccess;

public class DatabaseSuggestionsDataAccess : IDatabaseSuggestionsDataAccess
{
    private readonly PubTrackerDbContext _dbContext;

    public DatabaseSuggestionsDataAccess(PubTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private IQueryable<DatabaseSuggestion> Suggestions(int userId) =>
        _dbContext.DatabaseSuggestions.Select(s => new DatabaseSuggestion
        {
            Id = s.Id,
            Name = s.Name,
            WebsiteUrl = s.WebsiteUrl,
            VoteCount = _dbContext.DatabaseSuggestionVotes.Count(v => v.SuggestionId == s.Id),
            HasVoted = _dbContext.DatabaseSuggestionVotes.Any(v => v.SuggestionId == s.Id && v.UserId == userId)
        });

    public async Task<Paged<DatabaseSuggestion>> GetSuggestionsPageAsync(int userId, int page, int pageSize)
    {
        var totalCount = await _dbContext.DatabaseSuggestions.CountAsync();
        var items = await Suggestions(userId)
            .OrderByDescending(s => s.VoteCount)
            .ThenBy(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return new Paged<DatabaseSuggestion> { Items = items, TotalCount = totalCount };
    }

    public async Task<DatabaseSuggestion> CreateOrVoteAsync(int userId, string name, string websiteUrl, string reason)
    {
        var lowerName = name.ToLower();
        var existingId = await _dbContext.DatabaseSuggestions
            .Where(s => s.Name.ToLower() == lowerName)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync();

        // ponytail: no unique index on lower(name), so two simultaneous first suggestions of the
        // same name can both insert. Harmless duplicate row; add an expression index if it shows up.
        var suggestionId = existingId ?? await InsertAsync(userId, name, websiteUrl, reason);
        await AddVoteAsync(userId, suggestionId);
        return await Suggestions(userId).SingleAsync(s => s.Id == suggestionId);
    }

    private async Task<int> InsertAsync(int userId, string name, string websiteUrl, string reason)
    {
        var entity = new DatabaseSuggestionEntity
        {
            Name = name,
            WebsiteUrl = websiteUrl,
            Reason = reason,
            CreatedByUserId = userId
        };
        _dbContext.DatabaseSuggestions.Add(entity);
        await _dbContext.SaveChangesAsync();
        return entity.Id;
    }

    public async Task<bool> AddVoteAsync(int userId, int suggestionId)
    {
        if (!await _dbContext.DatabaseSuggestions.AnyAsync(s => s.Id == suggestionId))
        {
            return false;
        }

        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO database_suggestion_votes (suggestion_id, user_id) VALUES ({suggestionId}, {userId}) ON CONFLICT DO NOTHING");
        return true;
    }

    public async Task RemoveVoteAsync(int userId, int suggestionId)
    {
        await _dbContext.DatabaseSuggestionVotes
            .Where(v => v.SuggestionId == suggestionId && v.UserId == userId)
            .ExecuteDeleteAsync();
    }
}
