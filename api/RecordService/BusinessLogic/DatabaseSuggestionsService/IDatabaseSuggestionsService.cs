using RecordData;

namespace RecordService.BusinessLogic.DatabaseSuggestionsService;

public interface IDatabaseSuggestionsService
{
    Task<Paged<DatabaseSuggestion>> GetSuggestionsPageAsync(int userId, int page, int pageSize);
    Task<DatabaseSuggestion> SuggestAsync(int userId, string name, string websiteUrl, string? reason);
    Task<bool> AddVoteAsync(int userId, int suggestionId);
    Task RemoveVoteAsync(int userId, int suggestionId);
}
