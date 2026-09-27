using Microsoft.Extensions.DependencyInjection;
using RecordData;
using RecordService.BusinessLogic.RecordPollingService;
using RecordService.DataAccess;

namespace test;

/// <summary>
/// Regression coverage for the EF Core rewrite of the DataAccess layer (previously raw
/// Npgsql/NpgsqlCommand), specifically the paths not already exercised by
/// SearchQueryPollingEndToEndTests/CombinedDigestEndToEndTests: the Keycloak
/// provision/link/select branches, plain update/delete, and the targeted-poll sweep (the only
/// caller of GetOtherPendingDigestsForUserAsync). These exist to catch raw-SQL/EF translation
/// bugs (e.g. RETURNING-clause composition, column casing) that a compile-time check can't.
///
/// Own PubTrackerWebApplicationFactory instance/Postgres container - see
/// CombinedDigestEndToEndTests's doc comment for why each class needs its own.
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class EfRewriteCoverageTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public EfRewriteCoverageTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetOrProvisionByKeycloakSubAsync_InsertsSelectsAndLinksByEmail()
    {
        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();

        // Branch 1: no matching sub or email -> inserts a new user.
        var created = await usersDataAccess.GetOrProvisionByKeycloakSubAsync(
            "sub-1", "sub1@example.com", "Sub One", "sub-one");
        Assert.True(created.Id > 0);

        // Branch 2: same sub again -> selects the same row, doesn't duplicate.
        var reselected = await usersDataAccess.GetOrProvisionByKeycloakSubAsync(
            "sub-1", "sub1@example.com", "Sub One", "sub-one");
        Assert.Equal(created.Id, reselected.Id);

        // Branch 3: pre-existing user (no sub yet) with a matching email -> links by email
        // instead of inserting a duplicate.
        var preExisting = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Pre Existing",
            Username = "pre-existing",
            Email = "linked@example.com"
        });
        var linked = await usersDataAccess.GetOrProvisionByKeycloakSubAsync(
            "sub-2", "linked@example.com", "Linked User", "linked-user");
        Assert.Equal(preExisting.Id, linked.Id);
    }

    [Fact]
    public async Task UpdateUserAsync_And_SoftDeleteUserAsync_PersistChanges()
    {
        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();

        var user = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Original Name",
            Username = "original-username",
            Email = "original@example.com"
        });

        await usersDataAccess.UpdateUserAsync(user.Id, new User
        {
            Name = "Updated Name",
            Username = "updated-username",
            Email = "updated@example.com"
        });

        var updated = await usersDataAccess.GetUserByIdAsync(user.Id);
        Assert.Equal("Updated Name", updated.Name);
        Assert.Equal("updated-username", updated.Username);
        Assert.Equal("updated@example.com", updated.Email);
        Assert.False(updated.IsMarkedForDeletion);

        await usersDataAccess.SoftDeleteUserAsync(user.Id);

        var deleted = await usersDataAccess.GetUserByIdAsync(user.Id);
        Assert.True(deleted.IsMarkedForDeletion);
        Assert.NotNull(deleted.DeletionRequestedAt);
    }

    [Fact]
    public async Task UnsubscribeAsync_RemovesSubscription_AndIsIdempotent()
    {
        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var searchQueriesDataAccess = scope.ServiceProvider.GetRequiredService<ISearchQueriesDataAccess>();

        var user = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Unsub User",
            Username = "unsub-user",
            Email = "unsub-user@example.com"
        });

        var searchQuery = await searchQueriesDataAccess.SubscribeAsync(user.Id, $"{FakeLiteratureSourceProvider.TestUrl}?case=unsub");

        var beforeUnsub = await usersDataAccess.GetSearchQueriesByUserAsync(user.Id);
        Assert.Contains(beforeUnsub, sq => sq.Id == searchQuery.Id);

        var removed = await searchQueriesDataAccess.UnsubscribeAsync(user.Id, searchQuery.Id);
        Assert.True(removed);

        var afterUnsub = await usersDataAccess.GetSearchQueriesByUserAsync(user.Id);
        Assert.DoesNotContain(afterUnsub, sq => sq.Id == searchQuery.Id);

        var removedAgain = await searchQueriesDataAccess.UnsubscribeAsync(user.Id, searchQuery.Id);
        Assert.False(removedAgain);
    }

    [Fact]
    public async Task TargetedPoll_SweepsOtherAlreadyPendingQuery_ForSameUser()
    {
        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var searchQueriesDataAccess = scope.ServiceProvider.GetRequiredService<ISearchQueriesDataAccess>();
        var pollingService = scope.ServiceProvider.GetRequiredService<IRecordPollingService>();

        var user = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Sweep User",
            Username = "sweep-user",
            Email = "sweep-user@example.com"
        });

        var queryA = await searchQueriesDataAccess.SubscribeAsync(user.Id, $"{FakeLiteratureSourceProvider.TestUrl}?case=sweep-a");
        var queryB = await searchQueriesDataAccess.SubscribeAsync(user.Id, FakePedroLiteratureSourceProvider.TestUrl);

        // Poll both together first so B's records get persisted, and this same combined-digest
        // send advances the user's watermark for B past those records' first_seen_at.
        await pollingService.PollAllSearchQueriesAsync();
        Assert.Single(_factory.EmailSender.SentEmails, e => e.ToEmail == user.Email);
        _factory.EmailSender.SentEmails.Clear();

        // Roll the user's B watermark back to before those records were first seen, simulating
        // "not yet included in a digest" without a second fetch from B's source.
        await searchQueriesDataAccess.UpdateUserDigestWatermarkAsync(user.Id, queryB.Id, DateTime.UtcNow.AddDays(-10));

        // Targeted poll of A only - B must still be swept in via GetOtherPendingDigestsForUserAsync,
        // with no re-fetch of B from its source.
        var results = await pollingService.PollSearchQueriesAsync(new[] { queryA.Id });
        Assert.Single(results);
        Assert.True(results[0].IsSuccessful, results[0].ErrorMessage);

        var sentEmail = Assert.Single(_factory.EmailSender.SentEmails, e => e.ToEmail == user.Email);
        Assert.Contains("Fake Pedro Record 1", sentEmail.HtmlBody);
    }
}
