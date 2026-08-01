using RecordData;

namespace RecordService.DataAccess;

public class SearchQueriesDataAccess : ISearchQueriesDataAccess
{
    // This is a placeholder. In a real implementation, this would interact with the database
    private static List<SearchQuery> _searchQueries = new();

    public async Task<SearchQuery?> GetSearchQueryByIdAsync(int searchQueryId)
    {
        return await Task.FromResult(
            _searchQueries.FirstOrDefault(s => s.Id == searchQueryId)
        );
    }

    public async Task<List<int>> GetUserSubscribersForQueryAsync(int searchQueryId)
    {
        var searchQuery = _searchQueries.FirstOrDefault(s => s.Id == searchQueryId);
        if (searchQuery?.Subscribers == null)
        {
            return await Task.FromResult(new List<int>());
        }

        // Convert string subscribers to int (assuming they're stored as strings but represent user IDs)
        var userIds = searchQuery.Subscribers
            .Where(s => int.TryParse(s, out _))
            .Select(int.Parse)
            .ToList();

        return await Task.FromResult(userIds);
    }
}
