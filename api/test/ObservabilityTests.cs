using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using RecordData;
using RecordService;
using RecordService.BusinessLogic.DigestOutboxService;
using RecordService.BusinessLogic.DigestService;
using RecordService.DataAccess;
using RecordService.DataAccess.DigestOutbox;
using RecordService.Workers;
using Sentry;
using Sentry.Protocol;

namespace test;

/// <summary>
/// Silent failures page the owner: Sentry events carry no personal data, each worker pings its
/// heartbeat after a good cycle (and not after a failed one), and the Brevo quota alert fires once
/// per UTC day at 80% of Email:DailySendLimit.
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class ObservabilityTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public ObservabilityTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void SentryScrubber_RemovesUserIdentity_RequestData_EmailsAndUnsubscribeTokens()
    {
        var sentryEvent = new SentryEvent(new InvalidOperationException("Brevo send failed for ada@example.com: rejected"))
        {
            Message = new SentryMessage { Message = "Could not email {Email}", Formatted = "Could not email ada@example.com", Params = ["ada@example.com"] },
            User = { Id = "42", Email = "ada@example.com", Username = "ada", IpAddress = "203.0.113.9" },
            Request =
            {
                Url = "https://pubtracker.org/api/v1/subscriptions/unsubscribe/abc123.def456",
                Data = """{"email":"ada@example.com"}""",
                Cookies = "session=secret",
                QueryString = "email=ada@example.com"
            }
        };
        sentryEvent.Request.Headers["Authorization"] = "Bearer secret";
        sentryEvent.SentryExceptions = [new SentryException { Type = "X", Value = "failed for ada@example.com" }];

        SentryScrubber.Scrub(sentryEvent);

        Assert.Null(sentryEvent.User.Id);
        Assert.Null(sentryEvent.User.Email);
        Assert.Null(sentryEvent.User.Username);
        Assert.Null(sentryEvent.User.IpAddress);
        Assert.Null(sentryEvent.Request.Data);
        Assert.Null(sentryEvent.Request.Cookies);
        Assert.Null(sentryEvent.Request.QueryString);
        Assert.Empty(sentryEvent.Request.Headers);
        Assert.Equal("https://pubtracker.org/api/v1/subscriptions/unsubscribe/[token]", sentryEvent.Request.Url);
        Assert.Equal("Could not email [email]", sentryEvent.Message!.Formatted);
        Assert.Null(sentryEvent.Message.Params);
        Assert.Equal("failed for [email]", sentryEvent.SentryExceptions!.Single().Value);
    }

    [Fact]
    public async Task Heartbeat_PingsConfiguredUrl_ThrottlesRepeats_AndSurvivesFailures()
    {
        var handler = new CountingHandler(HttpStatusCode.OK);
        var pinger = new HeartbeatPinger(new HttpClient(handler), "https://hc.test/ping/abc", TimeSpan.FromMinutes(5), NullLogger.Instance);

        await pinger.PingAsync();
        await pinger.PingAsync(); // inside the minimum interval

        Assert.Equal(1, handler.Calls);

        var failing = new CountingHandler(HttpStatusCode.InternalServerError);
        var failingPinger = new HeartbeatPinger(new HttpClient(failing), "https://hc.test/ping/abc", TimeSpan.Zero, NullLogger.Instance);
        await failingPinger.PingAsync(); // must not throw
        await failingPinger.PingAsync(); // with no minimum interval, the next cycle tries again
        Assert.Equal(2, failing.Calls);

        // A failing URL is still throttled: one attempt per interval, not one per worker pass.
        var throttled = new CountingHandler(HttpStatusCode.InternalServerError);
        var throttledPinger = new HeartbeatPinger(new HttpClient(throttled), "https://hc.test/ping/abc", TimeSpan.FromMinutes(5), NullLogger.Instance);
        await throttledPinger.PingAsync();
        await throttledPinger.PingAsync();
        Assert.Equal(1, throttled.Calls);

        var unconfigured = new CountingHandler(HttpStatusCode.OK);
        await new HeartbeatPinger(new HttpClient(unconfigured), null, TimeSpan.Zero, NullLogger.Instance).PingAsync();
        Assert.Equal(0, unconfigured.Calls);
    }

    [Fact]
    public async Task OutboxWorker_PingsAfterAGoodPass_AndStaysSilentWhenThePassThrows()
    {
        var handler = new CountingHandler(HttpStatusCode.OK);
        var heartbeat = new HeartbeatPinger(new HttpClient(handler), "https://hc.test/ping/outbox", TimeSpan.Zero, NullLogger.Instance);

        // A real scope factory: the pass succeeds against the test database.
        var good = new DigestOutboxBackgroundService(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DigestOutboxBackgroundService>.Instance, TimeSpan.FromSeconds(60), heartbeat);
        await RunOnePassAsync(good);
        Assert.Equal(1, handler.Calls);

        // A scope factory that cannot resolve the service: the pass throws, so no ping, and a
        // heartbeat monitor would raise the alert once pings stop.
        var broken = new DigestOutboxBackgroundService(
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DigestOutboxBackgroundService>.Instance, TimeSpan.FromSeconds(60), heartbeat);
        await RunOnePassAsync(broken);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task OutboxService_ReportsSendFailuresOfTheLastPass_SoTheWorkerSkipsItsHeartbeat()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var connectionString = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")!;
        var sender = new FakeEmailSender();
        var service = new DigestOutboxService(
            scope.ServiceProvider.GetRequiredService<IDigestOutboxDataAccess>(), users, sender,
            new UnsubscribeTokens("k", "http://x"), new ConfigurationBuilder().Build(), NullLogger<DigestOutboxService>.Instance);

        await QueueDigestsAsync(users, connectionString, "hb-fail", 1);
        sender.FailForAddresses.Add("hb-fail-0@example.com");
        await service.ProcessDueAsync();
        Assert.Equal(1, service.LastPassSendFailures);

        sender.FailForAddresses.Clear();
        await service.ProcessDueAsync(utcNow: DateTime.UtcNow.AddHours(1)); // past the retry backoff
        Assert.Equal(0, service.LastPassSendFailures);
    }

    [Fact]
    public async Task QuotaAlert_FiresOnceAtEightyPercentOfTheDailyLimit()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var connectionString = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")!;
        var logger = new CapturingLogger();
        var service = new DigestOutboxService(
            scope.ServiceProvider.GetRequiredService<IDigestOutboxDataAccess>(), users, new FakeEmailSender(),
            new UnsubscribeTokens("k", "http://x"),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Email:DailySendLimit"] = "5" }).Build(),
            logger);

        // A day no other test uses, so the once-per-day guard and the sent-row count start clean.
        var day = new DateTime(2041, 3, 3, 12, 0, 0, DateTimeKind.Utc);
        await QueueDigestsAsync(users, connectionString, "quota-a", 3);
        await service.ProcessDueAsync(utcNow: day);
        Assert.Empty(logger.Errors); // 3 of 5 = 60%

        await QueueDigestsAsync(users, connectionString, "quota-b", 1);
        await service.ProcessDueAsync(utcNow: day); // sends the 4th; the next pass sees 4 of 5 = 80%
        await service.ProcessDueAsync(utcNow: day);
        var alert = Assert.Single(logger.Errors);
        Assert.Contains("80%", alert);

        await QueueDigestsAsync(users, connectionString, "quota-c", 1);
        await service.ProcessDueAsync(utcNow: day); // limit reached; still only one alert that day
        Assert.Single(logger.Errors);
    }

    private static async Task RunOnePassAsync(BackgroundService worker)
    {
        using var cts = new CancellationTokenSource();
        await worker.StartAsync(cts.Token);
        await Task.Delay(500); // the first pass runs at once; the next one is a minute away
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);
    }

    private static async Task QueueDigestsAsync(IUsersDataAccess users, string connectionString, string prefix, int count)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        for (var i = 0; i < count; i++)
        {
            var user = await users.CreateUserAsync(new User { Name = $"{prefix}-{i}", Username = $"{prefix}-{i}", Email = $"{prefix}-{i}@example.com" });
            await using var command = new NpgsqlCommand(
                $"INSERT INTO digest_outbox (user_id, subject, html_body, claims, next_attempt_at, created_at) VALUES ({user.Id}, 's', 'b', '[]', NOW(), NOW())", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed class CountingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Calls;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private sealed class CapturingLogger : ILogger<DigestOutboxService>
    {
        public List<string> Errors { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
            {
                Errors.Add(formatter(state, exception));
            }
        }
    }
}
