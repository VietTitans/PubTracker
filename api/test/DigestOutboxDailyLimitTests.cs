using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.BusinessLogic.DigestOutboxService;
using RecordService.BusinessLogic.RecordPollingService;
using RecordService.DataAccess;
using RecordService.DTOs.SearchQueryDto;

namespace test;

/// <summary>
/// Email:DailySendLimit caps digests sent per UTC day (Brevo free tier shares 300/day with
/// Keycloak's emails). Rows over the cap are never leased, so they keep their attempt count and
/// go out once the UTC day rolls over.
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class DigestOutboxDailyLimitTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public DigestOutboxDailyLimitTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<int> QueueDigestAsync(string name)
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
            new CreateSearchQueryDto { TargetUrl = $"{FakeLiteratureSourceProvider.TestUrl}?case={name}" });
        response.EnsureSuccessStatusCode();
        await _factory.SkipBaselinePollAsync();
        var created = await response.Content.ReadFromJsonAsync<SearchQueryResponseDto>();
        await scope.ServiceProvider.GetRequiredService<IRecordPollingService>().PollSearchQueryAsync(created!.Id);
        return user.Id;
    }

    private async Task<int> DrainAsync(DateTime utcNow)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IDigestOutboxService>().ProcessDueAsync(utcNow: utcNow);
    }

    [Fact]
    public async Task Burst_SendsOnlyDailyLimitToday_AndTheRestAfterUtcMidnight_WithoutConsumingAttempts()
    {
        var config = _factory.Services.GetRequiredService<IConfiguration>();
        Assert.Null(config["Email:DailySendLimit"]); // default (250) applies unless a test sets it
        config["Email:DailySendLimit"] = "2";
        try
        {

            var userIds = new List<int>();
            foreach (var name in new[] { "limit-a", "limit-b", "limit-c" })
            {
                userIds.Add(await QueueDigestAsync(name));
            }

            var now = DateTime.UtcNow;
            Assert.Equal(2, await DrainAsync(now));
            Assert.Equal(0, await DrainAsync(now)); // limit reached: nothing more today

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PubTrackerDbContext>();
                var deferred = await db.DigestOutbox.AsNoTracking().SingleAsync(o => o.SentAt == null);
                Assert.Equal(0, deferred.Attempts);
                Assert.Null(deferred.FailedAt);
                Assert.True(deferred.NextAttemptAt <= now);
            }

            var nextDay = now.Date.AddDays(1).AddMinutes(1);
            Assert.Equal(1, await DrainAsync(nextDay));
            Assert.Equal(3, _factory.EmailSender.SentEmails.Count(e => e.ToEmail.StartsWith("limit-")));
        }
        finally
        {
            config["Email:DailySendLimit"] = null;
        }
    }
}
