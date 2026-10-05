using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.BusinessLogic.RecordPollingService;
using RecordService.DataAccess;
using RecordService.DTOs.SearchQueryDto;

namespace test;

/// <summary>
/// Exposes a real race in RecordPollingService: nothing locks between reading a subscriber's
/// digest watermark (GetUserDigestWatermarksForQueryAsync) and writing it back after a
/// successful send (UpdateUserDigestWatermarkAsync). In production this can happen when the
/// scheduled RecordPollingBackgroundService fires at the same moment as a manual re-check of the
/// same search query; both read the same stale watermark, both compute the same "new since
/// watermark" records, and both send a digest, so the subscriber gets the same digest twice.
///
/// This test asserts the CORRECT behavior (exactly one digest) and is expected to fail against
/// the current code, which has no lock/idempotency check preventing the duplicate send. No fix
/// is applied here; this is deliberately a red test, per the user's request to see the gap
/// fail first before deciding how to fix it (Postgres advisory lock, SELECT ... FOR UPDATE, an
/// idempotency key, etc. are all options to weigh afterward).
///
/// Uses its own PubTrackerWebApplicationFactory instance for the same reason the other
/// e2e test classes do; a fresh Postgres container and fresh fakes.
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class ConcurrentPollRaceTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public ConcurrentPollRaceTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ConcurrentPolls_SameSearchQuery_ShouldNotSendDuplicateDigestEmail()
    {
        using var setupScope = _factory.Services.CreateScope();
        var usersDataAccess = setupScope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var user = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Race User",
            Username = "race-user",
            Email = "race-user@example.com"
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User-Id", user.Id.ToString());
        var createResponse = await client.PostAsJsonAsync("/api/v1/SearchQueries",
            new CreateSearchQueryDto { TargetUrl = $"{FakeLiteratureSourceProvider.TestUrl}?case=poll-race" });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<SearchQueryResponseDto>();
        var searchQueryId = created!.Id;

        // Two independent scopes, each with its own scoped PubTrackerDbContext; genuinely
        // simulates two overlapping requests (e.g. the scheduled poll and a manual re-check)
        // racing to poll and dispatch a digest for the same search query, rather than one
        // serialized call sharing state.
        using var scopeA = _factory.Services.CreateScope();
        using var scopeB = _factory.Services.CreateScope();
        var pollingServiceA = scopeA.ServiceProvider.GetRequiredService<IRecordPollingService>();
        var pollingServiceB = scopeB.ServiceProvider.GetRequiredService<IRecordPollingService>();

        await Task.WhenAll(
            pollingServiceA.PollSearchQueryAsync(searchQueryId),
            pollingServiceB.PollSearchQueryAsync(searchQueryId));

        // The subscriber should get exactly one digest for this batch of new records, not one
        // per concurrent poll that happened to race for the same watermark.
        Assert.Single(_factory.EmailSender.SentEmails, e => e.ToEmail == user.Email);
    }
}
