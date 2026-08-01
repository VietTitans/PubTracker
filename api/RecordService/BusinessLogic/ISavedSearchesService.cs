using RecordData;

namespace RecordService.BusinessLogic;

public interface ISavedSearchesService
{
    Task<List<SavedSearch>> GetSavedSearchesByUserAsync(int userId, int page = 1, int pageSize = 20);
    Task<SavedSearch?> GetSavedSearchByIdAsync(int savedSearchId);
    Task<SavedSearch> CreateSavedSearchAsync(int userId, int searchQueryId);
    Task<SavedSearch> UpdateSavedSearchAsync(int savedSearchId, int searchQueryId);
    Task DeleteSavedSearchAsync(int savedSearchId);
}
