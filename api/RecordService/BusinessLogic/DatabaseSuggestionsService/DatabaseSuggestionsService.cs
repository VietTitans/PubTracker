using RecordData;
using RecordService.Authentication;
using RecordService.DataAccess;

namespace RecordService.BusinessLogic.DatabaseSuggestionsService;

public class DatabaseSuggestionsService : IDatabaseSuggestionsService
{
    private readonly IDatabaseSuggestionsDataAccess _dataAccess;

    public DatabaseSuggestionsService(IDatabaseSuggestionsDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    public async Task<Paged<DatabaseSuggestion>> GetSuggestionsPageAsync(Caller caller, int userId, int page, int pageSize)
    {
        caller.RequireSelf(userId);
        return await _dataAccess.GetSuggestionsPageAsync(userId, page, pageSize);
    }

    public async Task<DatabaseSuggestion> SuggestAsync(Caller caller, int userId, string name, string websiteUrl, string? reason)
    {
        caller.RequireSelf(userId);
        return await _dataAccess.CreateOrVoteAsync(userId, name.Trim(), websiteUrl.Trim(), reason?.Trim() ?? string.Empty);
    }

    public async Task<bool> AddVoteAsync(Caller caller, int userId, int suggestionId)
    {
        caller.RequireSelf(userId);
        return await _dataAccess.AddVoteAsync(userId, suggestionId);
    }

    public async Task RemoveVoteAsync(Caller caller, int userId, int suggestionId)
    {
        caller.RequireSelf(userId);
        await _dataAccess.RemoveVoteAsync(userId, suggestionId);
    }
}
