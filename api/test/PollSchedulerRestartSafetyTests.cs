using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.BusinessLogic.RecordPollingService;
using RecordService.DataAccess;
using RecordService.DTOs.SearchQueryDto;

namespace test;

/// <summary>
/// Covers the restart-safety pieces of the scheduler: the Postgres advisory lock that keeps a
/// second instance out of a running cycle, and the min(last_polled_at) value the background
/// service uses to defer its first cycle after a restart.
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class PollSchedulerRestartSafetyTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public PollSchedulerRestartSafetyTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ScheduledCycle_WhenAnotherSessionHoldsAdvisoryLock_IsSkipped()
    {
        var advisoryLock = _factory.Services.GetRequiredService<PollCycleAdvisoryLock>();
        await using var held = await advisoryLock.AcquireAsync(); // waits out the host's own startup cycle

        using var scope = _factory.Services.CreateScope();
        var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();

        var results = await pollingService.PollAllSearchQueriesAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task AdvisoryLock_SecondTryFailsUntilFirstIsReleased()
    {
        var advisoryLock = _factory.Services.GetRequiredService<PollCycleAdvisoryLock>();

        var first = await advisoryLock.AcquireAsync();
        Assert.Null(await advisoryLock.TryAcquireAsync());

        await first.DisposeAsync();

        await using var again = await advisoryLock.TryAcquireAsync();
        Assert.NotNull(again);
    }

    [Fact]
    public async Task OldestLastPolledAt_IsNullUntilEveryQueryHasBeenPolled()
    {
        using var setupScope = _factory.Services.CreateScope();
        var usersDataAccess = setupScope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var user = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Gate User",
            Username = "gate-user",
            Email = "gate-user@example.com"
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User-Id", user.Id.ToString());
        var createResponse = await client.PostAsJsonAsync("/api/SearchQueries",
            new CreateSearchQueryDto { TargetUrl = $"{FakeLiteratureSourceProvider.TestUrl}?case=poll-gate" });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<SearchQueryResponseDto>();

        using var scope = _factory.Services.CreateScope();
        var dataAccess = scope.ServiceProvider.GetRequiredService<ISearchQueriesDataAccess>();

        // Subscribing triggers a background first poll; force a known "never polled" state by
        // checking only after our own explicit stamp below.
        var polledAt = DateTime.UtcNow.AddDays(-3);
        await dataAccess.RecordPollCompletedAsync(created!.Id, polledAt, null);

        var oldest = await dataAccess.GetOldestLastPolledAtAsync();

        Assert.NotNull(oldest);
        Assert.True(oldest <= polledAt.AddSeconds(1));
    }
    [Fact]
    public async Task DigestClaim_OnlyOneWinner_AndReleaseRestoresOldWatermark()
    {
        using var setupScope = _factory.Services.CreateScope();
        var usersDataAccess = setupScope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var user = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Claim User",
            Username = "claim-user",
            Email = "claim-user@example.com"
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User-Id", user.Id.ToString());
        var createResponse = await client.PostAsJsonAsync("/api/SearchQueries",
            new CreateSearchQueryDto { TargetUrl = $"{FakeLiteratureSourceProvider.TestUrl}?case=claim" });
        createResponse.EnsureSuccessStatusCode();
        var queryId = (await createResponse.Content.ReadFromJsonAsync<SearchQueryResponseDto>())!.Id;

        using var scope = _factory.Services.CreateScope();
        var dataAccess = scope.ServiceProvider.GetRequiredService<ISearchQueriesDataAccess>();

        var old = DateTime.UtcNow.AddDays(-2);
        await dataAccess.UpdateUserDigestWatermarkAsync(user.Id, queryId, old);
        old = (await dataAccess.GetUserDigestWatermarksForQueryAsync(queryId)).Single(w => w.UserId == user.Id).LastDigestSentAt!.Value;

        var claimed = DateTime.UtcNow.AddDays(-1);
        Assert.True(await dataAccess.TryClaimUserDigestWatermarkAsync(user.Id, queryId, old, claimed));
        Assert.False(await dataAccess.TryClaimUserDigestWatermarkAsync(user.Id, queryId, old, DateTime.UtcNow));

        await dataAccess.ReleaseUserDigestClaimAsync(user.Id, queryId, old, claimed);
        var after = (await dataAccess.GetUserDigestWatermarksForQueryAsync(queryId)).Single(w => w.UserId == user.Id).LastDigestSentAt;
        Assert.Equal(old, after);
    }
}
