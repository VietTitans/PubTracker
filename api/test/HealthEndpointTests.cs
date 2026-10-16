using System.Net;

namespace test;

/// <summary>The deploy pipeline's post-deploy check calls this endpoint anonymously.</summary>
[Collection("PubTrackerWebApplicationFactory")]
public class HealthEndpointTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public HealthEndpointTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_WithoutAuth_ReturnsOkWhenDatabaseReachable()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
