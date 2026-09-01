using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.BusinessLogic.RecordPollingService;
using RecordService.DataAccess;
using RecordService.DTOs.SearchQueryDto;

namespace test;

/// <summary>
/// End-to-end coverage of the full journey: create a search query over HTTP, persist it,
/// then poll it and persist discovered records - and specifically that the last_digest_sent_at
/// watermark (see RecordPollingService.PollAllSearchQueriesAsync) prevents a second poll from
/// re-counting already-seen records as new.
/// </summary>
public class SearchQueryPollingEndToEndTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public SearchQueryPollingEndToEndTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact(Skip = "Skipping until the test environment is properly configured")]
    public async Task CreateSearchQuery_ThenPoll_PersistsRecords_AndSecondPollFindsNothingNew()
    {
        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var user = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Test User",
            Username = "test-user",
            Email = "test-user@example.com"
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User-Id", user.Id.ToString());

        var createResponse = await client.PostAsJsonAsync("/api/SearchQueries", new CreateSearchQueryDto
        {
            TargetUrl = FakeLiteratureSourceProvider.TestUrl
        });
        createResponse.EnsureSuccessStatusCode();

        var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();

        var firstPollResults = await pollingService.PollAllSearchQueriesAsync();
        var firstResult = Assert.Single(firstPollResults);
        Assert.True(firstResult.IsSuccessful);
        Assert.Equal(2, firstResult.NewRecordCount);

        var sentEmail = Assert.Single(_factory.EmailSender.SentEmails);
        Assert.Equal(user.Email, sentEmail.ToEmail);

        var secondPollResults = await pollingService.PollAllSearchQueriesAsync();
        var secondResult = Assert.Single(secondPollResults);
        Assert.True(secondResult.IsSuccessful);
        Assert.Equal(0, secondResult.NewRecordCount);

        Assert.Single(_factory.EmailSender.SentEmails);
    }
}
