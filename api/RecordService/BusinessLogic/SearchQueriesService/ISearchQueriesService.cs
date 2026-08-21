using RecordData;

namespace RecordService.BusinessLogic.SearchQueriesService;

public interface ISearchQueriesService
{
    Task<SearchQuery?> GetSearchQueryByIdAsync(int searchQueryId);
    Task<List<int>> GetUserSubscribersForQueryAsync(int searchQueryId);
    Task<SearchQuery> SubscribeAsync(int userId, string targetUrl);
}
