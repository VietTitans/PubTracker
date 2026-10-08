using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.BusinessLogic.DigestOutboxService;
using RecordService.BusinessLogic.RecordPollingService;
using RecordService.DataAccess;
using RecordService.DTOs.SearchQueryDto;

namespace test;

/// <summary>
/// The digest outbox: the watermark claim and the queued digest commit together, so a digest
/// survives a crash before send, retries with backoff, and a permanent failure releases the
/// watermark so the next poll re-includes the records.
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class DigestOutboxTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public DigestOutboxTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(User User, int SearchQueryId)> CreateSubscriberAsync(string name, string urlCase)
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
        var created = await response.Content.ReadFromJsonAsync<SearchQueryResponseDto>();
        return (user, created!.Id);
    }

    [Fact]
    public async Task DigestQueuedBeforeSend_IsStillDeliveredByLaterDrain()
    {
        var (user, searchQueryId) = await CreateSubscriberAsync("outbox-crash", "outbox-crash");

        using (var scope = _factory.Services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IRecordPollingService>().PollSearchQueryAsync(searchQueryId);
            Assert.True(result!.IsSuccessful, result.ErrorMessage);

            // Simulated crash: process dies after the poll queued the digest, before any send.
            var dbContext = scope.ServiceProvider.GetRequiredService<PubTrackerDbContext>();
            Assert.Equal(1, await dbContext.DigestOutbox.CountAsync(o => o.UserId == user.Id && o.SentAt == null));
        }
        Assert.DoesNotContain(_factory.EmailSender.SentEmails, e => e.ToEmail == user.Email);

        await _factory.DrainOutboxAsync();
        Assert.Single(_factory.EmailSender.SentEmails, e => e.ToEmail == user.Email);

        await _factory.DrainOutboxAsync(); // already sent: no duplicate
        Assert.Single(_factory.EmailSender.SentEmails, e => e.ToEmail == user.Email);
    }

    [Fact]
    public async Task PermanentSendFailure_ReleasesWatermark_SoNextPollRequeuesDigest()
    {
        var (user, searchQueryId) = await CreateSubscriberAsync("outbox-fail", "outbox-fail");
        _factory.EmailSender.FailForAddresses.Add(user.Email);

        using var scope = _factory.Services.CreateScope();
        var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<PubTrackerDbContext>();

        await pollingService.PollSearchQueryAsync(searchQueryId);
        for (var attempt = 0; attempt < DigestOutboxService.MaxAttempts; attempt++)
        {
            await _factory.DrainOutboxAsync();
            await dbContext.Database.ExecuteSqlRawAsync("UPDATE digest_outbox SET next_attempt_at = now() WHERE sent_at IS NULL AND failed_at IS NULL");
        }

        Assert.Equal(1, await dbContext.DigestOutbox.AsNoTracking().CountAsync(o => o.UserId == user.Id && o.FailedAt != null));
        Assert.DoesNotContain(_factory.EmailSender.SentEmails, e => e.ToEmail == user.Email);

        // Watermark was released: the same records are pending again and a new poll re-queues them.
        _factory.EmailSender.FailForAddresses.Remove(user.Email);
        await pollingService.PollSearchQueryAsync(searchQueryId);
        await _factory.DrainOutboxAsync();
        Assert.Single(_factory.EmailSender.SentEmails, e => e.ToEmail == user.Email);
    }
}
