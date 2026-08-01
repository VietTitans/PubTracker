using RecordData;

namespace RecordService.BusinessLogic;

public interface ISearchQueriesService
{
    Task<SearchQuery?> GetSearchQueryByIdAsync(int searchQueryId);
    Task<List<int>> GetUserSubscribersForQueryAsync(int searchQueryId);
}
