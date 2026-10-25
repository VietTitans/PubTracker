using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.DataAccess;
using RecordService.DTOs.SearchQueryDto;

namespace test;

/// <summary>
/// End-to-end coverage for paginated list endpoints (?page=&amp;pageSize=, response wrapper
/// { items, page, pageSize, totalCount }). Responses are read as raw JSON so the tests pin the
/// wire shape rather than the server-side DTO types.
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class PaginationEndToEndTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public PaginationEndToEndTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task MySearchQueries_FirstPage_ReturnsRequestedSliceAndTotalCount()
    {
        var client = await CreateUserClientWithSearchQueriesAsync("first-page", queryCount: 5);

        var response = await client.GetAsync("/api/v1/Users/me/search-queries?page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.Equal(2, root.GetProperty("items").GetArrayLength());
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(2, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(5, root.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task MySearchQueries_LastPage_ReturnsRemainingItems()
    {
        var client = await CreateUserClientWithSearchQueriesAsync("last-page", queryCount: 5);

        var response = await client.GetAsync("/api/v1/Users/me/search-queries?page=3&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.Equal(1, root.GetProperty("items").GetArrayLength());
        Assert.Equal(3, root.GetProperty("page").GetInt32());
        Assert.Equal(5, root.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task MySearchQueries_PageBeyondLast_ReturnsEmptyItemsWithTotalCount()
    {
        var client = await CreateUserClientWithSearchQueriesAsync("beyond-last", queryCount: 5);

        var response = await client.GetAsync("/api/v1/Users/me/search-queries?page=10&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.Equal(0, root.GetProperty("items").GetArrayLength());
        Assert.Equal(10, root.GetProperty("page").GetInt32());
        Assert.Equal(5, root.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task UserSearchQueries_FirstPage_ReturnsRequestedSliceAndTotalCount()
    {
        var client = await CreateUserClientWithSearchQueriesAsync("by-user-id", queryCount: 5);
        var me = await client.GetAsync("/api/v1/Users/me");
        me.EnsureSuccessStatusCode();
        using var meBody = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        var userId = meBody.RootElement.GetProperty("id").GetInt32();

        var response = await client.GetAsync($"/api/v1/Users/{userId}/search-queries?page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.Equal(2, root.GetProperty("items").GetArrayLength());
        Assert.Equal(2, root.GetProperty("page").GetInt32());
        Assert.Equal(2, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(5, root.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task MySearchQueries_WalkingEveryPage_ReturnsEachItemOnceInAscendingIdOrder()
    {
        var client = await CreateUserClientWithSearchQueriesAsync("walk-pages", queryCount: 5);

        var ids = new List<int>();
        for (var page = 1; page <= 3; page++)
        {
            var response = await client.GetAsync($"/api/v1/Users/me/search-queries?page={page}&pageSize=2");
            response.EnsureSuccessStatusCode();
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            ids.AddRange(body.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()));
        }

        Assert.Equal(5, ids.Count);
        Assert.Equal(ids.Distinct().OrderBy(id => id), ids);
    }

    [Fact]
    public async Task MySearchQueries_WithoutPagingParameters_UsesDefaultPageSizeOf20()
    {
        var client = await CreateUserClientWithSearchQueriesAsync("defaults", queryCount: 1);

        var response = await client.GetAsync("/api/v1/Users/me/search-queries");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, body.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(20, body.RootElement.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task MySearchQueries_PageSizeAboveMaximum_IsClampedTo100()
    {
        var client = await CreateUserClientWithSearchQueriesAsync("clamp", queryCount: 1);

        var response = await client.GetAsync("/api/v1/Users/me/search-queries?pageSize=1000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(100, body.RootElement.GetProperty("pageSize").GetInt32());
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-1")]
    [InlineData("page=abc")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=abc")]
    public async Task MySearchQueries_InvalidPagingParameter_ReturnsBadRequestWithMessage(string query)
    {
        var client = await CreateUserClientWithSearchQueriesAsync($"invalid-{query.Replace("=", "-")}", queryCount: 0);

        var response = await client.GetAsync($"/api/v1/Users/me/search-queries?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Users_Pages_AreSlicedAndOrderedByAscendingId()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
            for (var i = 0; i < 3; i++)
            {
                await usersDataAccess.CreateUserAsync(new User
                {
                    Username = $"pagination-users-{i}",
                    Email = $"pagination-users-{i}@example.com"
                });
            }
        }
        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Debug-User-Role", "Admin");

        var (firstPage, firstTotal) = await GetPageAsync(admin, "/api/v1/Users?page=1&pageSize=2");
        var (secondPage, _) = await GetPageAsync(admin, "/api/v1/Users?page=2&pageSize=2");

        Assert.Equal(2, firstPage.Count);
        Assert.True(firstTotal >= 3);
        Assert.True(firstPage[0] < firstPage[1]);
        Assert.True(firstPage[1] < secondPage[0]);
    }

    [Fact]
    public async Task SearchQuerySubscribers_Pages_AreSlicedAndOrderedByAscendingUserId()
    {
        var sharedUrl = $"{FakeLiteratureSourceProvider.TestUrl}?case=pagination-subscribers";
        var firstSubscriber = await CreateUserClientWithSearchQueriesAsync("subscribers-0", queryCount: 0);
        var created = await firstSubscriber.PostAsJsonAsync("/api/v1/SearchQueries", new CreateSearchQueryDto { TargetUrl = sharedUrl });
        created.EnsureSuccessStatusCode();
        var searchQueryId = (await created.Content.ReadFromJsonAsync<SearchQueryResponseDto>())!.Id;
        for (var i = 1; i < 3; i++)
        {
            var subscriber = await CreateUserClientWithSearchQueriesAsync($"subscribers-{i}", queryCount: 0);
            (await subscriber.PostAsJsonAsync("/api/v1/SearchQueries", new CreateSearchQueryDto { TargetUrl = sharedUrl })).EnsureSuccessStatusCode();
        }
        var admin = _factory.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Debug-User-Role", "Admin");

        var (firstPage, totalCount) = await GetPageIdsAsync(admin, $"/api/v1/SearchQueries/{searchQueryId}/users?page=1&pageSize=2");
        var (secondPage, _) = await GetPageIdsAsync(admin, $"/api/v1/SearchQueries/{searchQueryId}/users?page=2&pageSize=2");

        Assert.Equal(3, totalCount);
        Assert.Equal(2, firstPage.Count);
        Assert.Single(secondPage);
        Assert.Equal(firstPage.Concat(secondPage).OrderBy(id => id), firstPage.Concat(secondPage));
    }

    [Fact]
    public async Task Sources_Pages_AreSlicedAndOrderedByAscendingId()
    {
        // Sources are created on first subscription; make sure two distinct ones exist.
        var client = await CreateUserClientWithSearchQueriesAsync("sources", queryCount: 1);
        (await client.PostAsJsonAsync("/api/v1/SearchQueries",
            new CreateSearchQueryDto { TargetUrl = FakePedroLiteratureSourceProvider.TestUrl })).EnsureSuccessStatusCode();

        var (firstPage, firstTotal) = await GetPageAsync(client, "/api/v1/Sources?page=1&pageSize=1");
        var (secondPage, _) = await GetPageAsync(client, "/api/v1/Sources?page=2&pageSize=1");

        Assert.Single(firstPage);
        Assert.True(firstTotal >= 2);
        Assert.Single(secondPage);
        Assert.True(firstPage[0] < secondPage[0]);
    }

    private static async Task<(List<int> Ids, int TotalCount)> GetPageAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = body.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()).ToList();
        return (ids, body.RootElement.GetProperty("totalCount").GetInt32());
    }

    /// <summary>Like <see cref="GetPageAsync"/>, for endpoints whose items are bare ids rather than objects.</summary>
    private static async Task<(List<int> Ids, int TotalCount)> GetPageIdsAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ids = body.RootElement.GetProperty("items").EnumerateArray().Select(i => i.GetInt32()).ToList();
        return (ids, body.RootElement.GetProperty("totalCount").GetInt32());
    }

    private async Task<HttpClient> CreateUserClientWithSearchQueriesAsync(string caseName, int queryCount)
    {
        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var user = await usersDataAccess.CreateUserAsync(new User
        {
            Username = $"pagination-{caseName}",
            Email = $"pagination-{caseName}@example.com"
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User-Id", user.Id.ToString());
        for (var i = 0; i < queryCount; i++)
        {
            var created = await client.PostAsJsonAsync("/api/v1/SearchQueries",
                new CreateSearchQueryDto { TargetUrl = $"{FakeLiteratureSourceProvider.TestUrl}?case=pagination-{caseName}-{i}" });
            created.EnsureSuccessStatusCode();
        }
        return client;
    }
}
