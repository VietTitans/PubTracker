using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.BusinessLogic.RecordPollingService;
using Microsoft.EntityFrameworkCore;
using RecordService.DataAccess;
using RecordService.DTOs.SearchQueryDto;

namespace test;

/// <summary>
/// Covers RecordPollingService.GenerateAuthorIntentionsAsync: the AI author-intention blurb is
/// generated once per genuinely new record (not per subscriber, not per search query the record
/// later gets linked to, see RecordsDataAccess.PersistSearchResultsAsync's xmax = 0 check), it
/// renders in the sent digest email once persisted, and a generation failure never blocks the
/// poll or the digest send (same failure-isolation contract as the per-query AI summary, covered
/// by DigestSummaryEndToEndTests).
///
/// Uses its own PubTrackerWebApplicationFactory instance for the same reason
/// CombinedDigestEndToEndTests/DigestSummaryEndToEndTests do; a fresh Postgres container and
/// fresh fakes so this class's assertions can't be polluted by other test classes' polls.
///
/// Uses FakeUniqueLiteratureSourceProvider (not FakeLiteratureSourceProvider) with a distinct
/// "recordSet" value per test: this class's three tests share one Postgres container (one
/// IClassFixture instance), so FakeLiteratureSourceProvider's hardcoded fake:1/fake:2 would let
/// whichever test runs first "claim" those records, leaving later tests to find them already
/// persisted instead of newly-inserted (and so never re-triggering generation); the exact bug
/// this file used to have. Each test also reads AuthorIntentionCalls from its own "calls so far"
/// baseline rather than asserting the whole (class-fixture-shared, ever-accumulating) list, so
/// assertions hold regardless of test execution order.
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class AuthorIntentionEndToEndTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public AuthorIntentionEndToEndTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PollAll_AuthorIntentionGeneratorSucceeds_IntentionIsPersistedAndSentInEmail()
    {
        // Shared FakeSummaryGenerator instance across this class's tests (class fixture) -
        // reset explicitly rather than relying on run order/defaults.
        _factory.SummaryGenerator.AuthorIntentionShouldThrow = false;
        _factory.SummaryGenerator.AuthorIntentionFixed = "The authors investigated whether exercise reduces pain.";

        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var user = await usersDataAccess.CreateUserAsync(new User
        {
            Username = "author-intention-user",
            Email = "author-intention-user@example.com"
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User-Id", user.Id.ToString());
        var createResponse = await client.PostAsJsonAsync("/api/v1/SearchQueries",
            new CreateSearchQueryDto { TargetUrl = $"{FakeUniqueLiteratureSourceProvider.BaseUrl}?recordSet=success" });
        createResponse.EnsureSuccessStatusCode();

        var callsBefore = _factory.SummaryGenerator.AuthorIntentionCalls.Count;

        var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();
        await _factory.SkipBaselinePollAsync();
        await pollingService.PollAllSearchQueriesAsync();
        await _factory.DrainOutboxAsync();

        Assert.Equal(
            new[] { "fakeunique:success:1", "fakeunique:success:2" },
            _factory.SummaryGenerator.AuthorIntentionCalls.Skip(callsBefore).Where(id => id.StartsWith("fakeunique:success:")).OrderBy(id => id));

        // FakeUniqueLiteratureSourceProvider's URL matches neither PubMed nor PEDro, so this goes
        // through DigestService.BuildHtmlBody's generic fallback, not DigestMessageFormatter -
        // hence "Author Intention: " here rather than DigestMessageFormatter's "Authors intent: ".
        var sentEmail = Assert.Single(_factory.EmailSender.SentEmails, e => e.ToEmail == user.Email);
        Assert.Contains("Author Intention: The authors investigated whether exercise reduces pain.", sentEmail.HtmlBody);
    }

    [Fact]
    public async Task PollAll_AuthorIntentionGeneratorThrows_DigestStillSendsWithoutIntention()
    {
        _factory.SummaryGenerator.AuthorIntentionFixed = string.Empty;
        _factory.SummaryGenerator.AuthorIntentionShouldThrow = true;

        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var user = await usersDataAccess.CreateUserAsync(new User
        {
            Username = "author-intention-failure-user",
            Email = "author-intention-failure-user@example.com"
        });

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User-Id", user.Id.ToString());
        var createResponse = await client.PostAsJsonAsync("/api/v1/SearchQueries",
            new CreateSearchQueryDto { TargetUrl = $"{FakeUniqueLiteratureSourceProvider.BaseUrl}?recordSet=failure" });
        createResponse.EnsureSuccessStatusCode();

        var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();
        await _factory.SkipBaselinePollAsync();
        var results = await pollingService.PollAllSearchQueriesAsync();
        await _factory.DrainOutboxAsync();

        Assert.Contains(results, r => r.IsSuccessful);

        var sentEmail = Assert.Single(_factory.EmailSender.SentEmails, e => e.ToEmail == user.Email);
        Assert.DoesNotContain("Author Intention:", sentEmail.HtmlBody);
    }

    [Fact]
    public async Task PollAll_RecordAlreadyPersistedViaAnotherQuery_DoesNotRegenerateAuthorIntention()
    {
        _factory.SummaryGenerator.AuthorIntentionShouldThrow = false;
        _factory.SummaryGenerator.AuthorIntentionFixed = "Shared record intention.";

        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var userA = await usersDataAccess.CreateUserAsync(new User
        {
            Username = "author-intention-dedup-user-a",
            Email = "author-intention-dedup-user-a@example.com"
        });
        var userB = await usersDataAccess.CreateUserAsync(new User
        {
            Username = "author-intention-dedup-user-b",
            Email = "author-intention-dedup-user-b@example.com"
        });

        // Both queries share the same recordSet (so FakeUniqueLiteratureSourceProvider resolves
        // them to the identical two external ids) but differ in the "query" param so they're two
        // distinct search_queries rows (source_id+target_url is unique); simulates two different
        // searches that happen to surface the same underlying paper. The second query's poll
        // should re-link the already-persisted records rather than inserting new ones.
        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("X-Debug-User-Id", userA.Id.ToString());
        var createResponseA = await clientA.PostAsJsonAsync("/api/v1/SearchQueries",
            new CreateSearchQueryDto { TargetUrl = $"{FakeUniqueLiteratureSourceProvider.BaseUrl}?recordSet=dedup-shared&query=a" });
        createResponseA.EnsureSuccessStatusCode();

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("X-Debug-User-Id", userB.Id.ToString());
        var createResponseB = await clientB.PostAsJsonAsync("/api/v1/SearchQueries",
            new CreateSearchQueryDto { TargetUrl = $"{FakeUniqueLiteratureSourceProvider.BaseUrl}?recordSet=dedup-shared&query=b" });
        createResponseB.EnsureSuccessStatusCode();

        var callsBefore = _factory.SummaryGenerator.AuthorIntentionCalls.Count;

        var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();

        // Both queries are brand new (never polled), so PollAllSearchQueriesAsync fetches A
        // first (inserting the shared records and generating their intentions), then B; whose
        // upsert finds both records already present and so must not trigger generation again.
        await pollingService.PollAllSearchQueriesAsync();
        await _factory.DrainOutboxAsync();

        Assert.Equal(
            new[] { "fakeunique:dedup-shared:1", "fakeunique:dedup-shared:2" },
            _factory.SummaryGenerator.AuthorIntentionCalls.Skip(callsBefore).Where(id => id.StartsWith("fakeunique:dedup-shared:")).OrderBy(id => id));
    }

    private async Task SeedRecordAsync(PubTrackerDbContext dbContext, string externalId, string? abstractText)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO records (external_id, title, abstract) VALUES ({externalId}, {externalId}, {abstractText})");
    }

    private async Task<string?> ReadIntentionAsync(PubTrackerDbContext dbContext, string externalId) =>
        await dbContext.Records.AsNoTracking().Where(r => r.ExternalId == externalId).Select(r => r.AuthorIntention).SingleAsync();

    private async Task<IRecordPollingService> SubscribeAndGetPollingServiceAsync(IServiceScope scope, string name)
    {
        var user = await scope.ServiceProvider.GetRequiredService<IUsersDataAccess>().CreateUserAsync(new User
        {
            Username = name,
            Email = $"{name}@example.com"
        });
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Debug-User-Id", user.Id.ToString());
        (await client.PostAsJsonAsync("/api/v1/SearchQueries",
            new CreateSearchQueryDto { TargetUrl = $"{FakeUniqueLiteratureSourceProvider.BaseUrl}?recordSet={name}" })).EnsureSuccessStatusCode();
        await _factory.SkipBaselinePollAsync();
        return scope.ServiceProvider.GetRequiredService<IRecordPollingService>();
    }

    [Fact]
    public async Task PollAll_OldRecordMissingIntention_IsBackfilled_AndRecordWithoutAbstractIsSkipped()
    {
        _factory.SummaryGenerator.AuthorIntentionShouldThrow = false;
        _factory.SummaryGenerator.AuthorIntentionFixed = "Backfilled intention.";

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PubTrackerDbContext>();
        await SeedRecordAsync(dbContext, "backfill-old:1", "An abstract.");
        await SeedRecordAsync(dbContext, "backfill-old:noabstract", null);
        var pollingService = await SubscribeAndGetPollingServiceAsync(scope, "backfill-old");

        await pollingService.PollAllSearchQueriesAsync();

        Assert.Equal("Backfilled intention.", await ReadIntentionAsync(dbContext, "backfill-old:1"));
        Assert.Null(await ReadIntentionAsync(dbContext, "backfill-old:noabstract"));
        Assert.DoesNotContain("backfill-old:noabstract", _factory.SummaryGenerator.AuthorIntentionCalls);
    }

    [Fact]
    public async Task PollAll_FailedIntention_StaysNull_AndIsRetriedOnNextPoll()
    {
        _factory.SummaryGenerator.AuthorIntentionFixed = "Second try worked.";

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PubTrackerDbContext>();
        await SeedRecordAsync(dbContext, "backfill-retry:1", "An abstract.");
        var pollingService = await SubscribeAndGetPollingServiceAsync(scope, "backfill-retry");

        _factory.SummaryGenerator.AuthorIntentionShouldThrow = true;
        await pollingService.PollAllSearchQueriesAsync();
        Assert.Null(await ReadIntentionAsync(dbContext, "backfill-retry:1"));

        _factory.SummaryGenerator.AuthorIntentionShouldThrow = false;
        await pollingService.PollAllSearchQueriesAsync();
        Assert.Equal("Second try worked.", await ReadIntentionAsync(dbContext, "backfill-retry:1"));
    }

    [Fact]
    public async Task PollAll_EmptyReply_IsStoredAsEmptyString_AndNotRetried()
    {
        _factory.SummaryGenerator.AuthorIntentionShouldThrow = false;
        _factory.SummaryGenerator.AuthorIntentionFixed = string.Empty;

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PubTrackerDbContext>();
        await SeedRecordAsync(dbContext, "backfill-empty:1", "An abstract.");
        var pollingService = await SubscribeAndGetPollingServiceAsync(scope, "backfill-empty");

        await pollingService.PollAllSearchQueriesAsync();
        Assert.Equal(string.Empty, await ReadIntentionAsync(dbContext, "backfill-empty:1"));

        _factory.SummaryGenerator.AuthorIntentionFixed = "Should not be used.";
        await pollingService.PollAllSearchQueriesAsync();
        Assert.Equal(string.Empty, await ReadIntentionAsync(dbContext, "backfill-empty:1"));
    }
}
