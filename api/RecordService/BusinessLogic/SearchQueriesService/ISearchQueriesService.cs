using RecordData;
using RecordService.Authentication;

namespace RecordService.BusinessLogic.SearchQueriesService;

public interface ISearchQueriesService
{
    // Every method takes the Caller and throws ForbiddenException when it may not act (see IUsersService).
    /// <summary>Admin, or a user subscribed to the query.</summary>
    Task<SearchQuery?> GetSearchQueryByIdAsync(Caller caller, int searchQueryId);
    Task<Paged<int>> GetUserSubscribersPageForQueryAsync(Caller caller, int searchQueryId, int page, int pageSize);
    Task<SearchQuery> SubscribeAsync(Caller caller, int userId, string targetUrl);
    Task<bool> UnsubscribeAsync(Caller caller, int userId, int searchQueryId);
}
