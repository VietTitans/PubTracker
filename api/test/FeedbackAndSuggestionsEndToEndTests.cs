using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.DataAccess;
using RecordService.DTOs;
using RecordService.DTOs.DatabaseSuggestionDto;

namespace test;

[Collection("PubTrackerWebApplicationFactory")]
public class FeedbackAndSuggestionsEndToEndTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public FeedbackAndSuggestionsEndToEndTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateClientForNewUserAsync(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<IUsersDataAccess>().CreateUserAsync(new User
        {
            Username = username,
            Email = $"{username}@example.com"
        });
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User-Id", user.Id.ToString());
        return client;
    }

    [Fact]
    public async Task Feedback_ValidSubmission_Returns204()
    {
        var client = await CreateClientForNewUserAsync("feedback-user");
        var response = await client.PostAsJsonAsync("/api/v1/Feedback", new { type = "idea", message = "Add dark mode" });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Feedback_UnknownType_Returns400()
    {
        var client = await CreateClientForNewUserAsync("feedback-bad-type");
        var response = await client.PostAsJsonAsync("/api/v1/Feedback", new { type = "rant", message = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Suggest_SameNameTwice_IsOneRowWithTwoVotes_AndVoteCanBeRemoved()
    {
        var alice = await CreateClientForNewUserAsync("suggest-alice");
        var bob = await CreateClientForNewUserAsync("suggest-bob");

        var first = await (await alice.PostAsJsonAsync("/api/v1/DatabaseSuggestions",
            new { name = "IEEE Xplore", websiteUrl = "https://ieeexplore.ieee.org", reason = "Engineering" }))
            .Content.ReadFromJsonAsync<DatabaseSuggestionResponseDto>();
        Assert.Equal(1, first!.VoteCount);
        Assert.True(first.HasVoted);

        var second = await (await bob.PostAsJsonAsync("/api/v1/DatabaseSuggestions",
            new { name = "ieee xplore", websiteUrl = "https://ieeexplore.ieee.org" }))
            .Content.ReadFromJsonAsync<DatabaseSuggestionResponseDto>();
        Assert.Equal(first.Id, second!.Id);
        Assert.Equal(2, second.VoteCount);

        // Voting again is idempotent.
        Assert.Equal(HttpStatusCode.NoContent, (await bob.PutAsync($"/api/v1/DatabaseSuggestions/{first.Id}/vote", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await bob.DeleteAsync($"/api/v1/DatabaseSuggestions/{first.Id}/vote")).StatusCode);

        var list = await (await alice.GetAsync("/api/v1/DatabaseSuggestions"))
            .Content.ReadFromJsonAsync<PagedResponseDto<DatabaseSuggestionResponseDto>>();
        var row = Assert.Single(list!.Items, s => s.Id == first.Id);
        Assert.Equal(1, row.VoteCount);
        Assert.True(row.HasVoted);
    }

    [Fact]
    public async Task Vote_UnknownSuggestion_Returns404()
    {
        var client = await CreateClientForNewUserAsync("vote-unknown");
        var response = await client.PutAsync("/api/v1/DatabaseSuggestions/999999/vote", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Suggest_NonHttpWebsite_Returns400()
    {
        var client = await CreateClientForNewUserAsync("suggest-bad-url");
        var response = await client.PostAsJsonAsync("/api/v1/DatabaseSuggestions",
            new { name = "Bad", websiteUrl = "javascript:alert(1)" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
