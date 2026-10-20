using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.BusinessLogic.DigestService;
using RecordService.BusinessLogic.RecordPollingService;
using RecordService.DataAccess;
using RecordService.DTOs.SearchQueryDto;
using RecordService.DTOs.UserDto;

namespace test;

[Collection("PubTrackerWebApplicationFactory")]
public class UnsubscribeEndToEndTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public UnsubscribeEndToEndTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(User User, int SearchQueryId, HttpClient Client)> CreateSubscribedUserAsync(string name, string targetUrl)
    {
        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<IUsersDataAccess>().CreateUserAsync(new User
        {
            Name = name,
            Username = name,
            Email = $"{name}@example.com"
        });
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User-Id", user.Id.ToString());
        var created = await (await client.PostAsJsonAsync("/api/v1/SearchQueries", new CreateSearchQueryDto { TargetUrl = targetUrl }))
            .Content.ReadFromJsonAsync<SearchQueryResponseDto>();
        return (user, created!.Id, client);
    }

    private async Task PollAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await _factory.SkipBaselinePollAsync();
        await scope.ServiceProvider.GetRequiredService<IRecordPollingService>().PollAllSearchQueriesAsync();
        await _factory.DrainOutboxAsync();
    }

    private UnsubscribeTokens Tokens => _factory.Services.GetRequiredService<UnsubscribeTokens>();

    [Fact]
    public async Task Digest_ContainsSubscriptionAndStopAllLinks_AndOneClickHeader()
    {
        var (user, queryId, _) = await CreateSubscribedUserAsync("unsub-content", FakeLiteratureSourceProvider.TestUrl + "&unsub=content");
        await PollAsync();

        var index = _factory.EmailSender.SentEmails.FindIndex(e => e.ToEmail == user.Email);
        Assert.True(index >= 0);
        var html = _factory.EmailSender.SentEmails[index].HtmlBody;
        Assert.Contains(Tokens.SubscriptionUrl(user.Id, queryId), html);
        Assert.Contains(Tokens.StopAllUrl(user.Id), html);
        Assert.Equal(Tokens.StopAllUrl(user.Id), _factory.EmailSender.UnsubscribeUrls[index]);
    }

    [Fact]
    public async Task OneClickPost_StopsAllDigests_ButKeepsSubscriptions()
    {
        var (user, _, client) = await CreateSubscribedUserAsync("unsub-stopall", FakeLiteratureSourceProvider.TestUrl + "&unsub=stopall");
        var anonymous = _factory.CreateClient();
        var token = Tokens.CreateStopAll(user.Id);

        var get = await anonymous.GetAsync($"/api/v1/subscribe/unsubscribe/{token}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.False((await client.GetFromJsonAsync<UserResponseDto>("/api/v1/Users/me"))!.DigestsStopped); // GET must not change state

        var post = await anonymous.PostAsync($"/api/v1/subscribe/unsubscribe/{token}", new FormUrlEncodedContent([new("List-Unsubscribe", "One-Click")]));
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        Assert.True((await client.GetFromJsonAsync<UserResponseDto>("/api/v1/Users/me"))!.DigestsStopped);

        await PollAsync();
        Assert.DoesNotContain(_factory.EmailSender.SentEmails, e => e.ToEmail == user.Email);
        var subs = await client.GetFromJsonAsync<PagedResponseShape>("/api/v1/Users/" + user.Id + "/search-queries");
        Assert.Equal(1, subs!.TotalCount);

        // Turning Digests back on via the API clears the flag.
        (await client.PutAsJsonAsync("/api/v1/Users/me/digests-stopped", new SetDigestsStoppedDto { Stopped = false })).EnsureSuccessStatusCode();
        Assert.False((await client.GetFromJsonAsync<UserResponseDto>("/api/v1/Users/me"))!.DigestsStopped);
    }

    [Fact]
    public async Task SubscriptionLink_RemovesOnlyThatSubscription()
    {
        var (user, queryId, client) = await CreateSubscribedUserAsync("unsub-one", FakeLiteratureSourceProvider.TestUrl + "&unsub=one");

        var post = await _factory.CreateClient().PostAsync($"/api/v1/subscribe/unsubscribe/{Tokens.CreateForSubscription(user.Id, queryId)}", null);
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);

        Assert.False((await client.GetFromJsonAsync<UserResponseDto>("/api/v1/Users/me"))!.DigestsStopped);
        var subs = await client.GetFromJsonAsync<PagedResponseShape>("/api/v1/Users/" + user.Id + "/search-queries");
        Assert.Equal(0, subs!.TotalCount);
    }

    [Fact]
    public async Task ForgedToken_IsRejected_AndChangesNothing()
    {
        var (user, _, client) = await CreateSubscribedUserAsync("unsub-forged", FakeLiteratureSourceProvider.TestUrl + "&unsub=forged");
        var anonymous = _factory.CreateClient();
        var forged = Tokens.CreateStopAll(user.Id + 1000).Split('.')[0] + "." + Tokens.CreateStopAll(user.Id).Split('.')[1];

        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsync($"/api/v1/subscribe/unsubscribe/{forged}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.GetAsync("/api/v1/subscribe/unsubscribe/nonsense")).StatusCode);
        Assert.False((await client.GetFromJsonAsync<UserResponseDto>("/api/v1/Users/me"))!.DigestsStopped);
    }

    private record PagedResponseShape(int TotalCount);
}
