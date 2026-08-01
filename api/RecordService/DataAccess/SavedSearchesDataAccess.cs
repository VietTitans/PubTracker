using RecordData;

namespace RecordService.DataAccess;

public class SavedSearchesDataAccess : ISavedSearchesDataAccess
{
    // This is a placeholder. In a real implementation, this would interact with the database
    private static List<SavedSearch> _savedSearches = new();

    public async Task<List<SavedSearch>> GetSavedSearchesByUserAsync(int userId, int page = 1, int pageSize = 20)
    {
        return await Task.FromResult(
            _savedSearches
                .Where(s => s.UserId == userId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList()
        );
    }

    public async Task<SavedSearch?> GetSavedSearchByIdAsync(int savedSearchId) 
    {
        return await Task.FromResult(
            _savedSearches.FirstOrDefault(s => s.Id == savedSearchId)
        );
    }

    public async Task<SavedSearch> CreateSavedSearchAsync(int userId, int searchQueryId)
    {
        var savedSearch = new SavedSearch
        {
            Id = _savedSearches.Count + 1,
            UserId = userId,
            SearchQueryId = searchQueryId,
            CreatedAt = DateTime.UtcNow
        };

        _savedSearches.Add(savedSearch);
        return await Task.FromResult(savedSearch);
    }

    public async Task<SavedSearch> UpdateSavedSearchAsync(int savedSearchId, int searchQueryId)
    {
        var savedSearch = _savedSearches.FirstOrDefault(s => s.Id == savedSearchId);
        if (savedSearch != null)
        {
            savedSearch.SearchQueryId = searchQueryId;
        }
        return await Task.FromResult(savedSearch!);
    }

    public async Task DeleteSavedSearchAsync(int savedSearchId)
    {
        var savedSearch = _savedSearches.FirstOrDefault(s => s.Id == savedSearchId);
        if (savedSearch != null)
        {
            _savedSearches.Remove(savedSearch);
        }
        await Task.CompletedTask;
    }
}
