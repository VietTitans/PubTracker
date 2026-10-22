using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.DataAccess;
using RecordService.DTOs.SearchQueryDto;

namespace test;

[Collection("PubTrackerWebApplicationFactory")]
public class SubscriptionCapTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public SubscriptionCapTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateClientAsync(string name)
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

    private static Task<HttpResponseMessage> SubscribeAsync(HttpClient client, string urlCase) =>
        client.PostAsJsonAsync("/api/v1/SearchQueries",
            new CreateSearchQueryDto { TargetUrl = $"{FakeLiteratureSourceProvider.TestUrl}?case={urlCase}" });

    [Fact]
    public async Task AtCapSucceeds_OverCapIsRejected_ResubscribeIsNotCounted_FreedSlotIsReusable()
    {
        var config = _factory.Services.GetRequiredService<IConfiguration>();
        Assert.Null(config["Subscriptions:MaxPerUser"]); // default (20) applies unless a test sets it
        config["Subscriptions:MaxPerUser"] = "2";
        try
        {
            var client = await CreateClientAsync("cap-user");

            var first = await SubscribeAsync(client, "cap-1");
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await SubscribeAsync(client, "cap-2")).StatusCode); // at cap

            var over = await SubscribeAsync(client, "cap-3");
            Assert.Equal(HttpStatusCode.UnprocessableEntity, over.StatusCode);
            Assert.Contains("at most 2", await over.Content.ReadAsStringAsync());

            // An existing query at the cap is "already subscribed", not a cap hit.
            Assert.Equal(HttpStatusCode.Conflict, (await SubscribeAsync(client, "cap-1")).StatusCode);

            // Another user is unaffected.
            Assert.Equal(HttpStatusCode.Created, (await SubscribeAsync(await CreateClientAsync("cap-other"), "cap-3")).StatusCode);

            // Freeing a slot lets this user subscribe again; the rejected attempt left nothing behind.
            var firstId = (await first.Content.ReadFromJsonAsync<SearchQueryResponseDto>())!.Id;
            Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/SearchQueries/{firstId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await SubscribeAsync(client, "cap-3")).StatusCode);
        }
        finally
        {
            config["Subscriptions:MaxPerUser"] = null;
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("lots")]
    public void InvalidCap_FailsStartup(string value)
    {
        using var factory = _factory.WithWebHostBuilder(b => b.UseSetting("Subscriptions:MaxPerUser", value));
        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }
}
