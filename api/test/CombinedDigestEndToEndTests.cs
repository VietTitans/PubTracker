using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.BusinessLogic.RecordPollingService;
using RecordService.DataAccess;
using RecordService.DTOs.SearchQueryDto;

namespace test;

/// <summary>
/// End-to-end coverage of the per-user combined digest behavior added on top of the base
/// polling flow covered by SearchQueryPollingEndToEndTests: one user subscribed to two
/// different sources gets exactly one combined email, and a failed send for one subscriber
/// doesn't block or duplicate delivery for any other subscriber of the same query.
///
/// Uses its own PubTrackerWebApplicationFactory instance (a fresh Postgres container and a
/// fresh FakeEmailSender) rather than sharing SearchQueryPollingEndToEndTests's, so its
/// multi-subscriber/multi-poll scenarios can't pollute that class's SentEmails-count
/// assertions or vice versa. Assertions here filter SentEmails by recipient email rather than
/// asserting on the list's total count, since both tests in this class also share one factory
/// instance with each other.
///
/// [Collection] groups this with every other test class that uses PubTrackerWebApplicationFactory
/// (see SearchQueryPollingEndToEndTests's doc comment) so xunit never initializes two factory
/// instances concurrently - they'd race on the process-wide environment variables
/// PubTrackerWebApplicationFactory configures itself through.
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class CombinedDigestEndToEndTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public CombinedDigestEndToEndTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OneUser_SubscribedToTwoQueries_PollAll_ReceivesOneCombinedEmail()
    {
        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var user = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Combined Digest User",
            Username = "combined-digest-user",
            Email = "combined-digest-user@example.com"
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User-Id", user.Id.ToString());

        var createdSearchQueryIds = new List<int>();
        foreach (var targetUrl in new[] { FakeLiteratureSourceProvider.TestUrl, FakePedroLiteratureSourceProvider.TestUrl })
        {
            var createResponse = await client.PostAsJsonAsync("/api/SearchQueries", new CreateSearchQueryDto { TargetUrl = targetUrl });
            createResponse.EnsureSuccessStatusCode();
            var created = await createResponse.Content.ReadFromJsonAsync<SearchQueryResponseDto>();
            createdSearchQueryIds.Add(created!.Id);
        }

        var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();

        // PollAllSearchQueriesAsync polls every query in this class's shared database, which by
        // now may include the isolation test's own query too (test methods within a class share
        // one Postgres container/class fixture) - filter to just the two this test created.
        var allResults = await pollingService.PollAllSearchQueriesAsync();
        var results = allResults.Where(r => createdSearchQueryIds.Contains(r.SearchQueryId)).ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.IsSuccessful, r.ErrorMessage));

        var sentEmail = Assert.Single(_factory.EmailSender.SentEmails, e => e.ToEmail == user.Email);

        Assert.Contains("Fake Record 1", sentEmail.HtmlBody);
        Assert.Contains("Fake Pedro Record 1", sentEmail.HtmlBody);
    }

    [Fact]
    public async Task TwoSubscribers_OneEmailFails_OnlyThatSubscriberIsRetried()
    {
        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var userA = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Subscriber A",
            Username = "subscriber-a",
            Email = "subscriber-a@example.com"
        });
        var userB = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Subscriber B",
            Username = "subscriber-b",
            Email = "subscriber-b@example.com"
        });

        int searchQueryId = 0;
        foreach (var (client, userId) in new[]
                 {
                     (_factory.CreateClient(), userA.Id),
                     (_factory.CreateClient(), userB.Id)
                 })
        {
            client.DefaultRequestHeaders.Add("X-Debug-User-Id", userId.ToString());
            // Distinct URL variant, not FakeLiteratureSourceProvider.TestUrl or
            // FakePedroLiteratureSourceProvider.TestUrl directly - OneUser_SubscribedToTwoQueries
            // already subscribes a user to both of those in this same class (same shared
            // Postgres container/class fixture), and reusing either would mean this test's poll
            // finds zero "new" records (the fake's records are fixed at "2 days ago"/"1 day
            // ago", already older than that other test's already-advanced last_polled_at).
            var createResponse = await client.PostAsJsonAsync("/api/SearchQueries",
                new CreateSearchQueryDto { TargetUrl = $"{FakeLiteratureSourceProvider.TestUrl}?case=isolation" });
            createResponse.EnsureSuccessStatusCode();
            var created = await createResponse.Content.ReadFromJsonAsync<SearchQueryResponseDto>();
            searchQueryId = created!.Id; // both subscriptions upsert the same shared query row
        }

        var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();

        // PollAllSearchQueriesAsync polls every query in this class's shared database, which by
        // now may include OneUser_SubscribedToTwoQueries's own queries too (test methods within
        // a class share one Postgres container/class fixture) - filter to just this test's query.
        _factory.EmailSender.FailForAddresses.Add(userB.Email);
        var firstResult = (await pollingService.PollAllSearchQueriesAsync()).Single(r => r.SearchQueryId == searchQueryId);
        Assert.False(firstResult.IsSuccessful); // one of the two subscribers' sends failed

        Assert.Single(_factory.EmailSender.SentEmails, e => e.ToEmail == userA.Email);
        Assert.DoesNotContain(_factory.EmailSender.SentEmails, e => e.ToEmail == userB.Email);

        _factory.EmailSender.FailForAddresses.Remove(userB.Email);
        var secondResult = (await pollingService.PollAllSearchQueriesAsync()).Single(r => r.SearchQueryId == searchQueryId);
        Assert.True(secondResult.IsSuccessful);

        // userA already received their email on the first poll and must not get a duplicate;
        // userB's failed send from the first poll is retried and now succeeds.
        Assert.Single(_factory.EmailSender.SentEmails, e => e.ToEmail == userA.Email);
        Assert.Single(_factory.EmailSender.SentEmails, e => e.ToEmail == userB.Email);
    }
}
