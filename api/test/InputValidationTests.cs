using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.DataAccess;
using RecordService.DTOs.SearchQueryDto;
using RecordService.DTOs.UserDto;

namespace test;

/// <summary>
/// Client input is rejected with a 400 and the { message, errors } contract, not a 500
/// or RFC 7807 ProblemDetails.
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class InputValidationTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public InputValidationTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateUserClientAsync(string name)
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
        return client;
    }

    private static async Task AssertBadRequestWithMessageAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("message").GetString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    public async Task CreateSearchQuery_BlankOrMalformedUrl_Returns400(string url)
    {
        var client = await CreateUserClientAsync("val-url-" + Guid.NewGuid().ToString("N")[..8]);

        var response = await client.PostAsJsonAsync("/api/v1/SearchQueries", new CreateSearchQueryDto { TargetUrl = url });

        await AssertBadRequestWithMessageAsync(response);
    }

    [Fact]
    public async Task CreateSearchQuery_OversizedUrl_Returns400()
    {
        var client = await CreateUserClientAsync("val-long-" + Guid.NewGuid().ToString("N")[..8]);
        var url = FakeLiteratureSourceProvider.TestUrl + "?case=" + new string('a', SearchQueryFieldLimits.TargetUrl);

        var response = await client.PostAsJsonAsync("/api/v1/SearchQueries", new CreateSearchQueryDto { TargetUrl = url });

        await AssertBadRequestWithMessageAsync(response);
    }

    [Theory]
    [InlineData("pubmed")]
    [InlineData("http://evil.example/?pubmed")]
    [InlineData("https://evil.example/pubmed.ncbi.nlm.nih.gov")]
    public async Task CreateSearchQuery_NonPubMedHostMentioningPubMed_Returns400Not500(string url)
    {
        var client = await CreateUserClientAsync("val-host-" + Guid.NewGuid().ToString("N")[..8]);

        var response = await client.PostAsJsonAsync("/api/v1/SearchQueries", new CreateSearchQueryDto { TargetUrl = url });

        await AssertBadRequestWithMessageAsync(response);
    }

    [Theory]
    [InlineData("", "ok", "ok@example.com")]
    [InlineData("Name", "", "ok@example.com")]
    [InlineData("Name", "ok", "not-an-email")]
    [InlineData("Name", "ok", "")]
    public async Task UpdateCurrentUser_InvalidFields_Returns400(string name, string username, string email)
    {
        var client = await CreateUserClientAsync("val-upd-" + Guid.NewGuid().ToString("N")[..8]);

        var response = await client.PutAsJsonAsync("/api/v1/Users/me",
            new UpdateUserDto { Name = name, Username = username, Email = email });

        await AssertBadRequestWithMessageAsync(response);
    }

    [Fact]
    public async Task UpdateCurrentUser_OversizedName_Returns400()
    {
        var client = await CreateUserClientAsync("val-upd-long-" + Guid.NewGuid().ToString("N")[..8]);

        var response = await client.PutAsJsonAsync("/api/v1/Users/me", new UpdateUserDto
        {
            Name = new string('a', UserFieldLimits.Name + 1),
            Username = "ok",
            Email = "ok@example.com"
        });

        await AssertBadRequestWithMessageAsync(response);
    }
}
