using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.BusinessLogic.RecordPollingService;
using RecordService.DataAccess;

namespace test;

/// <summary>
/// Covers the "source is down" path added after a review of what happens when a third-party
/// source (PubMed/PEDro) is unreachable: a failed poll persists SearchQuery.LastPollFailedAt
/// (surfaced to the frontend as a quiet "retrying automatically" status, see SearchQueryResponseDto)
/// without advancing LastPolledAt, and a subsequent successful poll clears it again.
///
/// Subscribes via ISearchQueriesDataAccess.SubscribeAsync directly rather than over HTTP -
/// SearchQueriesService.SubscribeAsync also kicks off a fire-and-forget initial poll 5s after
/// subscribing (see PollShortlyAfterSubscribeAsync), which would otherwise race this test's own
/// explicit poll calls.
///
/// Uses its own PubTrackerWebApplicationFactory instance for the same reason the other e2e test
/// classes do - a fresh Postgres container and fresh fakes.
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class SourceOutageEndToEndTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public SourceOutageEndToEndTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task FailedPoll_SetsLastPollFailedAt_AndClearsOnTheNextSuccessfulPoll()
    {
        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var searchQueriesDataAccess = scope.ServiceProvider.GetRequiredService<ISearchQueriesDataAccess>();
        var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();

        var user = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Outage User",
            Username = "outage-user",
            Email = "outage-user@example.com"
        });

        var targetUrl = $"{FakeLiteratureSourceProvider.TestUrl}?case=outage";
        var searchQuery = await searchQueriesDataAccess.SubscribeAsync(user.Id, targetUrl);

        _factory.LiteratureSourceProvider.FailForUrls.Add(targetUrl);

        await pollingService.PollAllSearchQueriesAsync();

        var afterFailure = await searchQueriesDataAccess.GetSearchQueryByIdAsync(searchQuery.Id);
        Assert.NotNull(afterFailure!.LastPollFailedAt);
        Assert.Null(afterFailure.SourceRecordCount);

        _factory.LiteratureSourceProvider.FailForUrls.Remove(targetUrl);

        await pollingService.PollAllSearchQueriesAsync();

        var afterRecovery = await searchQueriesDataAccess.GetSearchQueryByIdAsync(searchQuery.Id);
        Assert.Null(afterRecovery!.LastPollFailedAt);
        Assert.NotNull(afterRecovery.LastPolledAt);
    }
}
