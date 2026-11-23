using RecordData;
using RecordService.Authentication;

namespace RecordService.BusinessLogic.DatabaseSuggestionsService;

public interface IDatabaseSuggestionsService
{
    Task<Paged<DatabaseSuggestion>> GetSuggestionsPageAsync(Caller caller, int userId, int page, int pageSize);
    Task<DatabaseSuggestion> SuggestAsync(Caller caller, int userId, string name, string websiteUrl, string? reason);
    Task<bool> AddVoteAsync(Caller caller, int userId, int suggestionId);
    Task RemoveVoteAsync(Caller caller, int userId, int suggestionId);
}
