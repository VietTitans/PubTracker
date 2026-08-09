using RecordData;
using RecordService.DataAccess;

namespace RecordService.BusinessLogic;

public class SearchQueriesService : ISearchQueriesService
{
    private readonly ISearchQueriesDataAccess _dataAccess;

    public SearchQueriesService(ISearchQueriesDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    public async Task<SearchQuery?> GetSearchQueryByIdAsync(int searchQueryId)
    {
        return await _dataAccess.GetSearchQueryByIdAsync(searchQueryId);
    }

    public async Task<List<int>> GetUserSubscribersForQueryAsync(int searchQueryId)
    {
        return await _dataAccess.GetUserSubscribersForQueryAsync(searchQueryId);
    }

    public async Task<SearchQuery> SubscribeAsync(int userId, string targetUrl)
    {
        return await _dataAccess.SubscribeAsync(userId, targetUrl);
    }
}
