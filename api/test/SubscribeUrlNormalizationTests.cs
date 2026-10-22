using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.DataAccess;
using RecordService.DTOs.SearchQueryDto;

namespace test;

/// <summary>
/// A misspelled search URL and its corrected twin must dedupe to one search query
/// (FakeLiteratureSourceProvider canonicalizes "typo" to "fixed" like PubMed's spell correction).
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class SubscribeUrlNormalizationTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public SubscribeUrlNormalizationTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<SearchQueryResponseDto> SubscribeAsync(string name, string urlCase)
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
        var response = await client.PostAsJsonAsync("/api/v1/SearchQueries",
            new CreateSearchQueryDto { TargetUrl = $"{FakeLiteratureSourceProvider.TestUrl}?case={urlCase}" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SearchQueryResponseDto>())!;
    }

    [Fact]
    public async Task MisspelledAndCorrectedUrls_ShareOneSearchQuery()
    {
        var misspelled = await SubscribeAsync("norm-user-a", "norm-typo");
        var corrected = await SubscribeAsync("norm-user-b", "norm-fixed");

        Assert.Equal(misspelled.Id, corrected.Id);
        Assert.EndsWith("case=norm-fixed", misspelled.TargetUrl);
    }
}
