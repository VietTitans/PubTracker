using RecordData;

namespace RecordService.BusinessLogic.SearchQueriesService;

public interface ISearchQueriesService
{
    Task<SearchQuery?> GetSearchQueryByIdAsync(int searchQueryId);
    Task<List<int>> GetUserSubscribersForQueryAsync(int searchQueryId);
    Task<Paged<int>> GetUserSubscribersPageForQueryAsync(int searchQueryId, int page, int pageSize);
    Task<SearchQuery> SubscribeAsync(int userId, string targetUrl);
    Task<bool> UnsubscribeAsync(int userId, int searchQueryId);
}
