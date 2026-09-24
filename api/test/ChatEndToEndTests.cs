using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.BusinessLogic.RecordPollingService;
using RecordService.DataAccess;
using RecordService.DTOs.ChatDto;
using RecordService.DTOs.SearchQueryDto;

namespace test;

/// <summary>
/// Verifies POST /api/chat's retrieval is scoped to the requesting user's own tracked records -
/// the one property a single-user test can't catch, since a broken per-user join in
/// RecordsDataAccess.SearchSimilarRecordsAsync would still return a plausible 200.
///
/// [Collection] - see SearchQueryPollingEndToEndTests's doc comment.
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class ChatEndToEndTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public ChatEndToEndTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Ask_OnlyCitesTheRequestingUsersOwnTrackedRecords()
    {
        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();

        var userA = await usersDataAccess.CreateUserAsync(new User { Name = "User A", Username = "user-a", Email = "user-a@example.com" });
        var userB = await usersDataAccess.CreateUserAsync(new User { Name = "User B", Username = "user-b", Email = "user-b@example.com" });

        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("X-Debug-User-Id", userA.Id.ToString());
        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("X-Debug-User-Id", userB.Id.ToString());

        var queryA = await SubscribeAndPollAsync(clientA, pollingService, FakeLiteratureSourceProvider.TestUrl);
        var queryB = await SubscribeAndPollAsync(clientB, pollingService, FakePedroLiteratureSourceProvider.TestUrl);
        Assert.NotEqual(queryA, queryB);

        var responseA = await AskAsync(clientA, "What have I tracked?");
        var responseB = await AskAsync(clientB, "What have I tracked?");

        Assert.NotEmpty(responseA.Citations);
        Assert.NotEmpty(responseB.Citations);
        Assert.All(responseA.Citations, c => Assert.StartsWith("fake:", c.ExternalId));
        Assert.All(responseB.Citations, c => Assert.StartsWith("fakepedro:", c.ExternalId));

        var externalIdsA = responseA.Citations.Select(c => c.ExternalId).ToHashSet();
        var externalIdsB = responseB.Citations.Select(c => c.ExternalId).ToHashSet();
        Assert.Empty(externalIdsA.Intersect(externalIdsB));
    }

    private static async Task<int> SubscribeAndPollAsync(HttpClient client, IRecordPollingService pollingService, string targetUrl)
    {
        var createResponse = await client.PostAsJsonAsync("/api/SearchQueries", new CreateSearchQueryDto { TargetUrl = targetUrl });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<SearchQueryResponseDto>();

        var pollResult = await pollingService.PollSearchQueryAsync(created!.Id);
        Assert.True(pollResult!.IsSuccessful);

        return created.Id;
    }

    private static async Task<ChatResponseDto> AskAsync(HttpClient client, string question)
    {
        var response = await client.PostAsJsonAsync("/api/Chat", new ChatRequestDto { Question = question });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ChatResponseDto>())!;
    }
}
