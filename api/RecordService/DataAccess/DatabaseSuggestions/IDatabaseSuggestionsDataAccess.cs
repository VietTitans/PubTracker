using RecordData;

namespace RecordService.DataAccess;

public interface IDatabaseSuggestionsDataAccess
{
    Task<Paged<DatabaseSuggestion>> GetSuggestionsPageAsync(int userId, int page, int pageSize);

    /// <summary>
    /// Adds the suggestion, or, when one with the same name (case-insensitive) already exists, adds
    /// the user's vote to it instead. Either way the user ends up having voted. Returns the suggestion.
    /// </summary>
    Task<DatabaseSuggestion> CreateOrVoteAsync(int userId, string name, string websiteUrl, string reason);

    /// <summary>Idempotent. Returns false when the suggestion does not exist.</summary>
    Task<bool> AddVoteAsync(int userId, int suggestionId);

    Task RemoveVoteAsync(int userId, int suggestionId);
}
