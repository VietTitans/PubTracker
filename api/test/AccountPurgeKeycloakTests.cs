using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using RecordData;
using RecordService.BusinessLogic.UsersService;
using RecordService.DataAccess;
using RecordService.DataAccess.Keycloak;

namespace test;

/// <summary>
/// The purge deletes a user's Keycloak account (email and identity) before their database rows:
/// success and 404 both let the purge go on, any other failure keeps the user for the next cycle.
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class AccountPurgeKeycloakTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private const string RealmUrl = "http://keycloak:8080/auth/realms/science-alerts-saas";
    private readonly PubTrackerWebApplicationFactory _factory;

    public AccountPurgeKeycloakTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, "")]
    [InlineData(HttpStatusCode.NotFound, """{"error":"User not found"}""")]
    public async Task KeycloakDelete_SuccessAndUserNotFound_BothCountAsDeleted(HttpStatusCode deleteStatus, string body)
    {
        var handler = new StubHandler(deleteStatus, body);
        var sync = new KeycloakAdminUserSync(new HttpClient(handler), RealmUrl, "pubtracker-api-admin", "secret");

        await sync.DeleteUserAsync("sub-1");

        Assert.Equal(("DELETE", "http://keycloak:8080/auth/admin/realms/science-alerts-saas/users/sub-1", "Bearer admin-token"),
            (handler.Requests[^1].Method, handler.Requests[^1].Url, handler.Requests[^1].Authorization));
    }

    [Theory]
    [InlineData("""{"error":"Realm not found."}""")]
    [InlineData("")]
    public async Task KeycloakDelete_A404ThatIsNotUserNotFound_Throws(string body)
    {
        var sync = new KeycloakAdminUserSync(new HttpClient(new StubHandler(HttpStatusCode.NotFound, body)), RealmUrl, "pubtracker-api-admin", "secret");

        await Assert.ThrowsAsync<HttpRequestException>(() => sync.DeleteUserAsync("sub-1"));
    }

    [Fact]
    public async Task KeycloakDelete_ReusesOneAdminTokenForManyUsers()
    {
        var handler = new StubHandler(HttpStatusCode.NoContent, "");
        var sync = new KeycloakAdminUserSync(new HttpClient(handler), RealmUrl, "pubtracker-api-admin", "secret");

        await sync.DeleteUserAsync("sub-1");
        await sync.DeleteUserAsync("sub-2");
        await sync.DeleteUserAsync("sub-3");

        Assert.Equal(1, handler.Requests.Count(r => r.Url.EndsWith("/protocol/openid-connect/token")));
        Assert.Equal(3, handler.Requests.Count(r => r.Method == "DELETE"));
    }

    [Fact]
    public async Task KeycloakDelete_OtherErrors_Throw()
    {
        var sync = new KeycloakAdminUserSync(new HttpClient(new StubHandler(HttpStatusCode.InternalServerError, "")), RealmUrl, "pubtracker-api-admin", "secret");

        await Assert.ThrowsAsync<HttpRequestException>(() => sync.DeleteUserAsync("sub-1"));
    }

    [Fact]
    public async Task Purge_DeletesKeycloakFirst_AndKeepsOnlyTheUserWhoseKeycloakDeleteFailed()
    {
        using var scope = _factory.Services.CreateScope();
        var dataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var connectionString = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")!;
        var sync = new RecordingKeycloakSync { FailFor = "sub-purge-fails" };
        var service = new UsersService(dataAccess, sync, new FakeEmailSender(), new ConfigurationBuilder().Build(), NullLogger<UsersService>.Instance);

        var ok = await SeedDueUserAsync(dataAccess, connectionString, "purge-ok", "sub-purge-ok");
        var fails = await SeedDueUserAsync(dataAccess, connectionString, "purge-fails", "sub-purge-fails");
        var noSub = await SeedDueUserAsync(dataAccess, connectionString, "purge-nosub", null);
        var notDue = await dataAccess.CreateUserAsync(new User { Name = "Active", Username = "purge-active", Email = "purge-active@example.com" });

        var purged = await service.PurgeExpiredDeletedUsersAsync();

        Assert.Equal(2, purged);
        Assert.Null(await dataAccess.GetUserByIdAsync(ok.Id));
        Assert.Null(await dataAccess.GetUserByIdAsync(noSub.Id));
        Assert.NotNull(await dataAccess.GetUserByIdAsync(fails.Id)); // left for the next cycle
        Assert.NotNull(await dataAccess.GetUserByIdAsync(notDue.Id));
        Assert.Contains("sub-purge-ok", sync.Deleted);
        Assert.Contains("sub-purge-fails", sync.Attempted);
        Assert.Equal(2, sync.Attempted.Count); // the user without a Keycloak sub is never sent to Keycloak

        // The next cycle retries only the failed one, and succeeds once Keycloak recovers.
        sync.FailFor = null;
        Assert.Equal(1, await service.PurgeExpiredDeletedUsersAsync());
        Assert.Null(await dataAccess.GetUserByIdAsync(fails.Id));
        Assert.Contains("sub-purge-fails", sync.Deleted);
    }

    [Fact]
    public async Task Purge_StillCascadesSubscriptionsWatermarksAndOutboxRows()
    {
        using var scope = _factory.Services.CreateScope();
        var dataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var connectionString = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")!;
        var service = new UsersService(dataAccess, new RecordingKeycloakSync(), new FakeEmailSender(), new ConfigurationBuilder().Build(), NullLogger<UsersService>.Instance);

        var user = await SeedDueUserAsync(dataAccess, connectionString, "purge-cascade", "sub-purge-cascade");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "INSERT INTO sources (name, base_url) VALUES ('cascade-src', 'https://cascade.test') ON CONFLICT (name) DO NOTHING");
        await ExecuteAsync(connection, "INSERT INTO search_queries (source_id, target_url) SELECT id, 'https://cascade.test/q' FROM sources WHERE name = 'cascade-src'");
        await ExecuteAsync(connection, $"INSERT INTO user_search_queries (user_id, search_query_id) SELECT {user.Id}, id FROM search_queries WHERE target_url = 'https://cascade.test/q'");
        await ExecuteAsync(connection, $"INSERT INTO user_search_query_digests (user_id, search_query_id, last_digest_sent_at) SELECT {user.Id}, id, NOW() FROM search_queries WHERE target_url = 'https://cascade.test/q'");
        await ExecuteAsync(connection, $"INSERT INTO digest_outbox (user_id, subject, html_body, claims, next_attempt_at, created_at) VALUES ({user.Id}, 's', 'b', '[]', NOW(), NOW())");

        Assert.Equal(1, await service.PurgeExpiredDeletedUsersAsync());

        foreach (var table in new[] { "user_search_queries", "user_search_query_digests", "digest_outbox" })
        {
            await using var count = new NpgsqlCommand($"SELECT COUNT(*) FROM {table} WHERE user_id = {user.Id}", connection);
            Assert.Equal(0L, await count.ExecuteScalarAsync());
        }
    }

    [Fact]
    public async Task PurgeUser_ReChecksUnderTheLock_SoAUserWhoSignedBackInIsKeptAndKeycloakIsNotCalled()
    {
        using var scope = _factory.Services.CreateScope();
        var dataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var connectionString = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")!;
        var user = await SeedDueUserAsync(dataAccess, connectionString, "purge-signed-back-in", "sub-signed-back-in");
        // The user signs in after the purge listed them: sign-in clears the deletion mark.
        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await ExecuteAsync(connection, $"UPDATE users SET is_marked_for_deletion = false, deletion_requested_at = NULL WHERE id = {user.Id}");
        }

        var called = false;
        var purged = await dataAccess.PurgeUserAsync(user.Id, _ => { called = true; return Task.CompletedTask; });

        Assert.False(purged);
        Assert.False(called);
        Assert.NotNull(await dataAccess.GetUserByIdAsync(user.Id));
    }

    [Fact]
    public async Task PurgeUser_WhenTheExternalDeleteThrows_NothingIsDeleted()
    {
        using var scope = _factory.Services.CreateScope();
        var dataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var connectionString = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")!;
        var user = await SeedDueUserAsync(dataAccess, connectionString, "purge-rollback", "sub-rollback");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            dataAccess.PurgeUserAsync(user.Id, _ => throw new HttpRequestException("down")));

        Assert.NotNull(await dataAccess.GetUserByIdAsync(user.Id));
        await dataAccess.PurgeUserAsync(user.Id, _ => Task.CompletedTask); // leave this class's shared database clean
    }

    [Fact]
    public async Task Purge_StopsTheCycleAfterThreeFailuresInARow()
    {
        using var scope = _factory.Services.CreateScope();
        var dataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var connectionString = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")!;
        var sync = new RecordingKeycloakSync { FailForAll = true };
        var service = new UsersService(dataAccess, sync, new FakeEmailSender(), new ConfigurationBuilder().Build(), NullLogger<UsersService>.Instance);
        var seeded = new List<User>();
        for (var i = 0; i < 5; i++)
        {
            seeded.Add(await SeedDueUserAsync(dataAccess, connectionString, $"purge-outage-{i}", $"sub-outage-{i}"));
        }

        try
        {
            var purged = await service.PurgeExpiredDeletedUsersAsync();

            Assert.Equal(0, purged);
            Assert.Equal(3, sync.Attempted.Count(s => s.StartsWith("sub-outage-")));
        }
        finally
        {
            // They share this class's database with the other tests, which count Keycloak calls.
            foreach (var user in seeded)
            {
                await dataAccess.PurgeUserAsync(user.Id, _ => Task.CompletedTask);
            }
        }
    }

    private static async Task<User> SeedDueUserAsync(IUsersDataAccess dataAccess, string connectionString, string name, string? keycloakSub)
    {
        var user = await dataAccess.CreateUserAsync(new User { Name = name, Username = name, Email = $"{name}@example.com" });
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "UPDATE users SET is_marked_for_deletion = true, deletion_requested_at = @at, keycloak_sub = @sub WHERE id = @id", connection);
        command.Parameters.AddWithValue("at", DateTime.UtcNow.AddDays(-40));
        command.Parameters.AddWithValue("sub", (object?)keycloakSub ?? DBNull.Value);
        command.Parameters.AddWithValue("id", user.Id);
        await command.ExecuteNonQueryAsync();
        return user;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class RecordingKeycloakSync : IKeycloakUserSync
    {
        public string? FailFor { get; set; }
        public bool FailForAll { get; set; }
        public List<string> Attempted { get; } = new();
        public List<string> Deleted { get; } = new();

        public Task UpdateEmailAsync(string keycloakSub, string newEmail) => Task.CompletedTask;

        public Task DeleteUserAsync(string keycloakSub)
        {
            Attempted.Add(keycloakSub);
            if (FailForAll || keycloakSub == FailFor)
            {
                throw new HttpRequestException("Keycloak delete user failed (500)");
            }
            Deleted.Add(keycloakSub);
            return Task.CompletedTask;
        }
    }

    private sealed class StubHandler(HttpStatusCode deleteStatus, string deleteBody) : HttpMessageHandler
    {
        public readonly List<(string Method, string Url, string? Authorization)> Requests = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.Method.Method, request.RequestUri!.ToString(), request.Headers.Authorization?.ToString()));
            if (request.RequestUri.AbsolutePath.EndsWith("/protocol/openid-connect/token"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"access_token":"admin-token","expires_in":300}""", System.Text.Encoding.UTF8, "application/json")
                });
            }
            return Task.FromResult(new HttpResponseMessage(deleteStatus) { Content = new StringContent(deleteBody) });
        }
    }
}
