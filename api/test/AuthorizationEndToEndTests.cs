using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.DataAccess;
using RecordService.DTOs.SearchQueryDto;

namespace test;

/// <summary>
/// Regression coverage for the authorization fixes added after a security review found every
/// [Authorize] attribute on UsersController/SearchQueriesController commented out (anyone could
/// read/enumerate any user's data). Proves the ownership checks actually reject a different
/// authenticated user (via Forbid()) rather than just relying on manual reasoning about how the
/// dev "Smart" auth scheme forwards Forbid.
///
/// [Collection] groups this with every other test class that uses PubTrackerWebApplicationFactory
/// (see SearchQueryPollingEndToEndTests's doc comment) so xunit never initializes two factory
/// instances concurrently.
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class AuthorizationEndToEndTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public AuthorizationEndToEndTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UserCannotReadAnotherUsersSearchQueriesOrSearchQueryDetail()
    {
        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();

        var userA = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "User A",
            Username = "user-a-idor",
            Email = "user-a-idor@example.com"
        });
        var userB = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "User B",
            Username = "user-b-idor",
            Email = "user-b-idor@example.com"
        });

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("X-Debug-User-Id", userB.Id.ToString());
        var createResponse = await clientB.PostAsJsonAsync("/api/v1/SearchQueries",
            new CreateSearchQueryDto { TargetUrl = $"{FakeLiteratureSourceProvider.TestUrl}?case=idor" });
        createResponse.EnsureSuccessStatusCode();
        var searchQuery = await createResponse.Content.ReadFromJsonAsync<SearchQueryResponseDto>();

        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("X-Debug-User-Id", userA.Id.ToString());

        // A is neither B nor an Admin; both of B's endpoints must reject A.
        var otherUsersQueries = await clientA.GetAsync($"/api/v1/Users/{userB.Id}/search-queries");
        Assert.Equal(HttpStatusCode.Forbidden, otherUsersQueries.StatusCode);

        var otherSearchQuery = await clientA.GetAsync($"/api/v1/SearchQueries/{searchQuery!.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, otherSearchQuery.StatusCode);

        // B can read their own data through both endpoints.
        var ownQueries = await clientB.GetAsync($"/api/v1/Users/{userB.Id}/search-queries");
        Assert.Equal(HttpStatusCode.OK, ownQueries.StatusCode);

        var ownSearchQuery = await clientB.GetAsync($"/api/v1/SearchQueries/{searchQuery.Id}");
        Assert.Equal(HttpStatusCode.OK, ownSearchQuery.StatusCode);

        // A can still read their own (empty) search-query list.
        var aOwnQueries = await clientA.GetAsync($"/api/v1/Users/{userA.Id}/search-queries");
        Assert.Equal(HttpStatusCode.OK, aOwnQueries.StatusCode);
    }

    [Fact]
    public async Task UserCannotReadAnotherUsersProfile()
    {
        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();

        var userA = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "User A2",
            Username = "user-a2-idor",
            Email = "user-a2-idor@example.com"
        });
        var userB = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "User B2",
            Username = "user-b2-idor",
            Email = "user-b2-idor@example.com"
        });

        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("X-Debug-User-Id", userA.Id.ToString());

        var otherProfile = await clientA.GetAsync($"/api/v1/Users/{userB.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, otherProfile.StatusCode);

        var ownProfile = await clientA.GetAsync($"/api/v1/Users/{userA.Id}");
        Assert.Equal(HttpStatusCode.OK, ownProfile.StatusCode);
    }
}
