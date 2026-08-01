using RecordData;
using RecordService.DataAccess;

namespace RecordService.BusinessLogic;

public class SavedSearchesService : ISavedSearchesService
{
    private readonly ISavedSearchesDataAccess _dataAccess;

    public SavedSearchesService(ISavedSearchesDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    public async Task<List<SavedSearch>> GetSavedSearchesByUserAsync(int userId, int page = 1, int pageSize = 20)
    {
        return await _dataAccess.GetSavedSearchesByUserAsync(userId, page, pageSize);
    }

    public async Task<SavedSearch?> GetSavedSearchByIdAsync(int savedSearchId)
    {
        return await _dataAccess.GetSavedSearchByIdAsync(savedSearchId);
    }

    public async Task<SavedSearch> CreateSavedSearchAsync(int userId, int searchQueryId)
    {
        return await _dataAccess.CreateSavedSearchAsync(userId, searchQueryId);
    }

    public async Task<SavedSearch> UpdateSavedSearchAsync(int savedSearchId, int searchQueryId)
    {
        return await _dataAccess.UpdateSavedSearchAsync(savedSearchId, searchQueryId);
    }

    public async Task DeleteSavedSearchAsync(int savedSearchId)
    {
        await _dataAccess.DeleteSavedSearchAsync(savedSearchId);
    }
}
