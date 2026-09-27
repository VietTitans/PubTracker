using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RecordData;
using RecordService.BusinessLogic.UsersService;
using RecordService.DataAccess;

namespace test;

/// <summary>
/// End-to-end coverage of the soft-delete grace period (see
/// UsersDataAccess.DeletionGracePeriodDays): signing back in within the window undoes the
/// soft delete, signing back in after the window does not, and the purge worker only
/// hard-deletes accounts once the window has passed - cleanly, without violating the
/// user_search_queries/user_search_query_digests foreign keys.
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class UserDeletionGracePeriodTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public UserDeletionGracePeriodTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SigningBackInWithinGracePeriod_Reactivates_ButAfterGracePeriod_StaysDeleted_AndIsPurged()
    {
        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var usersService = scope.ServiceProvider.GetRequiredService<IUsersService>();
        var connectionString = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")!;

        var withinGraceUser = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Regretful User", Username = "regretful", Email = "regretful@example.com"
        });
        await MarkDeletedAsync(connectionString, withinGraceUser.Id, "sub-within-grace", DateTime.UtcNow.AddDays(-5));

        var pastGraceUser = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Too Late User", Username = "too-late", Email = "too-late@example.com"
        });
        await MarkDeletedAsync(connectionString, pastGraceUser.Id, "sub-past-grace", DateTime.UtcNow.AddDays(-20));
        var searchQueryId = await SeedSearchQuerySubscriptionAsync(connectionString, pastGraceUser.Id);

        var justDeletedUser = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Just Deleted User", Username = "just-deleted", Email = "just-deleted@example.com"
        });
        await MarkDeletedAsync(connectionString, justDeletedUser.Id, null, DateTime.UtcNow);

        // Signing back in within the grace period reactivates the account.
        var reactivated = await usersDataAccess.GetOrProvisionByKeycloakSubAsync(
            "sub-within-grace", withinGraceUser.Email, withinGraceUser.Name, withinGraceUser.Username);
        Assert.False(reactivated.IsMarkedForDeletion);
        Assert.Null(reactivated.DeletionRequestedAt);

        // Signing back in after the grace period leaves the account marked for deletion.
        var stillDeleted = await usersDataAccess.GetOrProvisionByKeycloakSubAsync(
            "sub-past-grace", pastGraceUser.Email, pastGraceUser.Name, pastGraceUser.Username);
        Assert.True(stillDeleted.IsMarkedForDeletion);

        var purgedCount = await usersService.PurgeExpiredDeletedUsersAsync();
        Assert.Equal(1, purgedCount);

        // Past its grace period and never reactivated -> hard-deleted, subscription cleaned up too.
        Assert.Null(await usersDataAccess.GetUserByIdAsync(pastGraceUser.Id));
        Assert.False(await SearchQuerySubscriptionExistsAsync(connectionString, pastGraceUser.Id, searchQueryId));

        // Reactivated -> untouched by the purge.
        var reactivatedAfterPurge = await usersDataAccess.GetUserByIdAsync(withinGraceUser.Id);
        Assert.NotNull(reactivatedAfterPurge);
        Assert.False(reactivatedAfterPurge.IsMarkedForDeletion);

        // Marked for deletion but not yet past its grace period -> untouched by the purge.
        var stillWithinGrace = await usersDataAccess.GetUserByIdAsync(justDeletedUser.Id);
        Assert.NotNull(stillWithinGrace);
        Assert.True(stillWithinGrace.IsMarkedForDeletion);
    }

    [Fact]
    public async Task SigningBackInWithinGracePeriod_Reactivates_WhenAccountWasNeverLinkedToKeycloakYet()
    {
        // Regression for a row that was soft-deleted before ever completing a first Keycloak
        // login (keycloak_sub still NULL) - GetOrProvisionByKeycloakSubAsync's email-fallback
        // linking branch must reactivate it too, not just the already-linked branch.
        using var scope = _factory.Services.CreateScope();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var connectionString = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")!;

        var user = await usersDataAccess.CreateUserAsync(new User
        {
            Name = "Never Linked User", Username = "never-linked", Email = "never-linked@example.com"
        });
        await MarkDeletedAsync(connectionString, user.Id, keycloakSub: null, DateTime.UtcNow.AddDays(-5));

        var reactivated = await usersDataAccess.GetOrProvisionByKeycloakSubAsync(
            "sub-first-login", user.Email, user.Name, user.Username);

        Assert.False(reactivated.IsMarkedForDeletion);
        Assert.Null(reactivated.DeletionRequestedAt);
    }

    private static async Task MarkDeletedAsync(string connectionString, int userId, string? keycloakSub, DateTime deletionRequestedAt)
    {
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new NpgsqlCommand(
            "UPDATE users SET is_marked_for_deletion = true, deletion_requested_at = @deletionRequestedAt, keycloak_sub = @keycloakSub WHERE id = @id",
            connection);
        command.Parameters.AddWithValue("@id", userId);
        command.Parameters.AddWithValue("@deletionRequestedAt", deletionRequestedAt);
        command.Parameters.AddWithValue("@keycloakSub", (object?)keycloakSub ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> SeedSearchQuerySubscriptionAsync(string connectionString, int userId)
    {
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        int searchQueryId;
        using (var command = new NpgsqlCommand(
            "INSERT INTO search_queries (target_url) VALUES (@targetUrl) RETURNING id", connection))
        {
            command.Parameters.AddWithValue("@targetUrl", $"https://example.com/{Guid.NewGuid()}");
            searchQueryId = (int)(await command.ExecuteScalarAsync())!;
        }

        using (var command = new NpgsqlCommand(
            "INSERT INTO user_search_queries (user_id, search_query_id) VALUES (@userId, @searchQueryId)", connection))
        {
            command.Parameters.AddWithValue("@userId", userId);
            command.Parameters.AddWithValue("@searchQueryId", searchQueryId);
            await command.ExecuteNonQueryAsync();
        }

        return searchQueryId;
    }

    private static async Task<bool> SearchQuerySubscriptionExistsAsync(string connectionString, int userId, int searchQueryId)
    {
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new NpgsqlCommand(
            "SELECT 1 FROM user_search_queries WHERE user_id = @userId AND search_query_id = @searchQueryId", connection);
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@searchQueryId", searchQueryId);
        return await command.ExecuteScalarAsync() != null;
    }
}
