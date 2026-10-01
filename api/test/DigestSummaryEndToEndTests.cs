using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.BusinessLogic.RecordPollingService;
using RecordService.DataAccess;
using RecordService.DTOs.SearchQueryDto;

namespace test;

/// <summary>
/// Covers the AI digest summary, generated once per search query per poll cycle in
/// RecordPollingService (not once per subscriber in DigestService) so that many subscribers of
/// the same query share one summary instead of paying for the same content N times: the
/// generated summary renders in the sent HTML, the same text reaches every subscriber of a
/// shared query from a single generator call, and a summary-generation failure never blocks the
/// digest send (same failure-isolation contract as a per-subscriber email-send failure, covered
/// by CombinedDigestEndToEndTests).
///
/// Uses its own PubTrackerWebApplicationFactory instance for the same reason
/// CombinedDigestEndToEndTests does - a fresh Postgres container and fresh fakes so this
/// class's assertions can't be polluted by other test classes' polls.
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class DigestSummaryEndToEndTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public DigestSummaryEndToEndTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PollAll_SummaryGeneratorSucceeds_SummaryIsPrependedToSentEmail()
    {
        // Both tests in this class share one FakeSummaryGenerator instance (class fixture), so
        // reset both flags here rather than relying on run order/defaults.
        _factory.SummaryGenerator.ShouldThrow = false;
        _factory.SummaryGenerator.FixedSummary = "Three new exercise trials were published this week.";

        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var user = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Summary User",
            Username = "summary-user",
            Email = "summary-user@example.com"
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User-Id", user.Id.ToString());
        var createResponse = await client.PostAsJsonAsync("/api/v1/SearchQueries",
            new CreateSearchQueryDto { TargetUrl = $"{FakeLiteratureSourceProvider.TestUrl}?case=summary-success" });
        createResponse.EnsureSuccessStatusCode();

        var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();
        await pollingService.PollAllSearchQueriesAsync();

        var sentEmail = Assert.Single(_factory.EmailSender.SentEmails, e => e.ToEmail == user.Email);
        Assert.Contains("AI Summary", sentEmail.HtmlBody);
        Assert.Contains("Three new exercise trials were published this week.", sentEmail.HtmlBody);
    }

    [Fact]
    public async Task PollAll_SummaryGeneratorThrows_DigestStillSendsWithoutSummary()
    {
        // Same shared-instance caveat as the other test in this class - reset explicitly.
        _factory.SummaryGenerator.FixedSummary = string.Empty;
        _factory.SummaryGenerator.ShouldThrow = true;

        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var user = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Summary Failure User",
            Username = "summary-failure-user",
            Email = "summary-failure-user@example.com"
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User-Id", user.Id.ToString());
        var createResponse = await client.PostAsJsonAsync("/api/v1/SearchQueries",
            new CreateSearchQueryDto { TargetUrl = $"{FakeLiteratureSourceProvider.TestUrl}?case=summary-failure" });
        createResponse.EnsureSuccessStatusCode();

        var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();
        var results = await pollingService.PollAllSearchQueriesAsync();

        Assert.Contains(results, r => r.IsSuccessful);
        Assert.Single(_factory.EmailSender.SentEmails, e => e.ToEmail == user.Email);
    }

    [Fact]
    public async Task PollAll_TwoSubscribersToSameQuery_GeneratesSummaryOnlyOnce()
    {
        _factory.SummaryGenerator.ShouldThrow = false;
        _factory.SummaryGenerator.FixedSummary = "Shared summary for both subscribers.";

        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var userA = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Shared Query User A",
            Username = "shared-query-user-a",
            Email = "shared-query-user-a@example.com"
        });
        var userB = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Shared Query User B",
            Username = "shared-query-user-b",
            Email = "shared-query-user-b@example.com"
        });

        var targetUrl = $"{FakeLiteratureSourceProvider.TestUrl}?case=shared-summary";
        foreach (var userId in new[] { userA.Id, userB.Id })
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Debug-User-Id", userId.ToString());
            var createResponse = await client.PostAsJsonAsync("/api/v1/SearchQueries", new CreateSearchQueryDto { TargetUrl = targetUrl });
            createResponse.EnsureSuccessStatusCode();
        }

        var callsBefore = _factory.SummaryGenerator.Calls.Count;

        var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();
        await pollingService.PollAllSearchQueriesAsync();

        Assert.Equal(callsBefore + 1, _factory.SummaryGenerator.Calls.Count);

        var emailA = Assert.Single(_factory.EmailSender.SentEmails, e => e.ToEmail == userA.Email);
        var emailB = Assert.Single(_factory.EmailSender.SentEmails, e => e.ToEmail == userB.Email);
        Assert.Contains("Shared summary for both subscribers.", emailA.HtmlBody);
        Assert.Contains("Shared summary for both subscribers.", emailB.HtmlBody);
    }
}
