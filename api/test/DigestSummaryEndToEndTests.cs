using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.BusinessLogic.RecordPollingService;
using RecordService.DataAccess;
using RecordService.DTOs.SearchQueryDto;

namespace test;

/// <summary>
/// Covers the AI digest summary added on top of DigestService.SendCombinedDigestsAsync: a
/// generated summary is prepended to the sent HTML, and a summary-generation failure never
/// blocks the digest send (same failure-isolation contract as a per-subscriber email-send
/// failure, covered by CombinedDigestEndToEndTests).
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
        var createResponse = await client.PostAsJsonAsync("/api/SearchQueries",
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
        var createResponse = await client.PostAsJsonAsync("/api/SearchQueries",
            new CreateSearchQueryDto { TargetUrl = $"{FakeLiteratureSourceProvider.TestUrl}?case=summary-failure" });
        createResponse.EnsureSuccessStatusCode();

        var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();
        var results = await pollingService.PollAllSearchQueriesAsync();

        Assert.Contains(results, r => r.IsSuccessful);
        Assert.Single(_factory.EmailSender.SentEmails, e => e.ToEmail == user.Email);
    }
}
