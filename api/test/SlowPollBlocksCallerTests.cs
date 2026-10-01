using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.BusinessLogic.RecordPollingService;
using RecordService.DataAccess;
using RecordService.DTOs.SearchQueryDto;

namespace test;

/// <summary>
/// Exposes that RecordPollingService.FetchOneAsync runs the entire pipeline synchronously -
/// external fetch, every author-intention LLM call, the digest-summary LLM call, and the email
/// send - before returning to its caller. Nothing hands this off to a background job; whoever
/// calls it (the poll endpoint, or the scheduled RecordPollingBackgroundService) blocks for the
/// full duration. A manual real run against 50 records already measured this at 15-52 real
/// seconds - here, FakeSummaryGenerator.Delay simulates that same per-call network latency in a
/// small, controlled, deterministic way, so the same shape of problem is provable without a
/// slow, flaky, real-LLM-dependent test.
///
/// This test asserts the CORRECT/desired behavior (the caller isn't blocked waiting on LLM/email
/// work), which would require handing that work off to a background queue. Deliberately left
/// red rather than fixed: the only caller of PollSearchQueryAsync that used to require a fast
/// HTTP response (POST /api/SearchQueries/{id}/poll) was manual-testing-only scaffolding and has
/// been deleted; the two remaining callers (the scheduled background poller, and the
/// already-fire-and-forget initial poll after subscribing - see
/// SearchQueriesService.PollShortlyAfterSubscribeAsync) don't need this method to return
/// quickly. A real background queue is real added complexity (it would also require
/// ConcurrentPollRaceTests to wait for the queue to drain instead of asserting immediately) not
/// worth taking on until an actual caller needs it.
///
/// Uses its own PubTrackerWebApplicationFactory instance for the same reason the other e2e test
/// classes do - a fresh Postgres container and fresh fakes.
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class SlowPollBlocksCallerTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public SlowPollBlocksCallerTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(Skip = "Known limitation, not currently fixed - see class doc comment. No caller needs a fast response today.")]
    public async Task PollSearchQuery_WithSlowSummaryGenerator_ShouldReturnQuicklyInsteadOfBlocking()
    {
        _factory.SummaryGenerator.Delay = TimeSpan.FromMilliseconds(250);

        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var user = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Slow Poll User",
            Username = "slow-poll-user",
            Email = "slow-poll-user@example.com"
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User-Id", user.Id.ToString());
        var createResponse = await client.PostAsJsonAsync("/api/v1/SearchQueries",
            new CreateSearchQueryDto { TargetUrl = $"{FakeLiteratureSourceProvider.TestUrl}?case=slow-poll" });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<SearchQueryResponseDto>();
        var searchQueryId = created!.Id;

        var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();

        var stopwatch = Stopwatch.StartNew();
        await pollingService.PollSearchQueryAsync(searchQueryId);
        stopwatch.Stop();

        // A caller (the poll endpoint, or the scheduled background service) should get control
        // back almost immediately, with the actual LLM/email work happening elsewhere. Today
        // it doesn't - the caller waits for the full pipeline (2 author-intention calls plus the
        // digest summary call, ~500ms+ at this Delay), proving there's no background hand-off.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(200),
            $"Expected PollSearchQueryAsync to return quickly (caller not blocked on LLM/email work), but it took {stopwatch.ElapsedMilliseconds}ms.");
    }
}
