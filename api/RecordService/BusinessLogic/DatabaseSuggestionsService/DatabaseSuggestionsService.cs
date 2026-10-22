using RecordData;
using RecordService.DataAccess;

namespace RecordService.BusinessLogic.DatabaseSuggestionsService;

public class DatabaseSuggestionsService : IDatabaseSuggestionsService
{
    private readonly IDatabaseSuggestionsDataAccess _dataAccess;

    public DatabaseSuggestionsService(IDatabaseSuggestionsDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    public async Task<Paged<DatabaseSuggestion>> GetSuggestionsPageAsync(int userId, int page, int pageSize)
    {
        return await _dataAccess.GetSuggestionsPageAsync(userId, page, pageSize);
    }

    public async Task<DatabaseSuggestion> SuggestAsync(int userId, string name, string websiteUrl, string? reason)
    {
        return await _dataAccess.CreateOrVoteAsync(userId, name.Trim(), websiteUrl.Trim(), reason?.Trim() ?? string.Empty);
    }

    public async Task<bool> AddVoteAsync(int userId, int suggestionId)
    {
        return await _dataAccess.AddVoteAsync(userId, suggestionId);
    }

    public async Task RemoveVoteAsync(int userId, int suggestionId)
    {
        await _dataAccess.RemoveVoteAsync(userId, suggestionId);
    }
}
