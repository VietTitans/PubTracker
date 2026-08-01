using RecordData;

namespace RecordService.DataAccess;

public interface ISearchQueriesDataAccess
{
    Task<SearchQuery?> GetSearchQueryByIdAsync(int searchQueryId);
    Task<List<int>> GetUserSubscribersForQueryAsync(int searchQueryId);
}
