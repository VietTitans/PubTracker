using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using RecordData;
using RecordService.BusinessLogic.UsersService;
using RecordService.DataAccess;

namespace test;

/// <summary>
/// Accounts untouched for 5 years get one warning email; if the user does not sign in within 30 days of
/// it, the next purge deletes the account. Signing in at any point cancels the countdown.
/// </summary>
[Collection("PubTrackerWebApplicationFactory")]
public class InactiveUserNoticeTests : IClassFixture<PubTrackerWebApplicationFactory>
{
    private readonly PubTrackerWebApplicationFactory _factory;

    public InactiveUserNoticeTests(PubTrackerWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OnlyUsersInactiveForFiveYears_AreWarned_AndOnlyOnce()
    {
        using var scope = _factory.Services.CreateScope();
        var usersService = scope.ServiceProvider.GetRequiredService<IUsersService>();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var db = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")!;

        var dormant = await CreateAsync(usersDataAccess, "dormant");
        var recent = await CreateAsync(usersDataAccess, "recent-ish");
        var leaving = await CreateAsync(usersDataAccess, "dormant-leaving");
        await SetAsync(db, dormant.Id, "last_active_at = now() - interval '5 years 1 day'");
        await SetAsync(db, recent.Id, "last_active_at = now() - interval '4 years 11 months'");
        await SetAsync(db, leaving.Id, "last_active_at = now() - interval '6 years', is_marked_for_deletion = true, deletion_requested_at = now()");

        await usersService.SendInactivityNoticesAsync();
        await usersService.SendInactivityNoticesAsync();

        var email = Assert.Single(_factory.EmailSender.SentEmails, e => e.ToEmail == dormant.Email);
        Assert.Contains("30 days", email.HtmlBody);
        Assert.DoesNotContain(_factory.EmailSender.SentEmails, e => e.ToEmail == recent.Email);
        Assert.DoesNotContain(_factory.EmailSender.SentEmails, e => e.ToEmail == leaving.Email);
    }

    [Fact]
    public async Task WarnedUser_WhoDoesNotSignIn_IsPurgedAfter30Days_ButNotBefore()
    {
        using var scope = _factory.Services.CreateScope();
        var usersService = scope.ServiceProvider.GetRequiredService<IUsersService>();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var db = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")!;

        var waiting = await CreateAsync(usersDataAccess, "warned-waiting");
        var expired = await CreateAsync(usersDataAccess, "warned-expired");
        await SetAsync(db, waiting.Id, "last_active_at = now() - interval '5 years 10 days', inactivity_notice_sent_at = now() - interval '29 days'");
        await SetAsync(db, expired.Id, "last_active_at = now() - interval '5 years 40 days', inactivity_notice_sent_at = now() - interval '31 days'");

        await usersService.PurgeExpiredDeletedUsersAsync();

        Assert.NotNull(await usersDataAccess.GetUserByIdAsync(waiting.Id));
        Assert.Null(await usersDataAccess.GetUserByIdAsync(expired.Id));
    }

    [Fact]
    public async Task SigningIn_RecordsActivity_AndCancelsTheCountdown()
    {
        using var scope = _factory.Services.CreateScope();
        var usersService = scope.ServiceProvider.GetRequiredService<IUsersService>();
        var usersDataAccess = scope.ServiceProvider.GetRequiredService<IUsersDataAccess>();
        var db = scope.ServiceProvider.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")!;

        var returning = await CreateAsync(usersDataAccess, "warned-returning");
        await SetAsync(db, returning.Id, "keycloak_sub = 'sub-returning', last_active_at = now() - interval '5 years 40 days', inactivity_notice_sent_at = now() - interval '31 days'");

        await usersService.GetOrProvisionByKeycloakSubAsync("sub-returning", returning.Email, returning.Username);
        await usersService.PurgeExpiredDeletedUsersAsync();

        Assert.NotNull(await usersDataAccess.GetUserByIdAsync(returning.Id));
        Assert.Equal(0L, await ScalarAsync(db, returning.Id, "(inactivity_notice_sent_at IS NOT NULL)::int"));
        Assert.Equal(1L, await ScalarAsync(db, returning.Id, "(last_active_at > now() - interval '1 minute')::int"));

        // A later warning cycle leaves the returning user alone.
        await usersService.SendInactivityNoticesAsync();
        Assert.DoesNotContain(_factory.EmailSender.SentEmails, e => e.ToEmail == returning.Email);
    }

    private static Task<User> CreateAsync(IUsersDataAccess usersDataAccess, string name) =>
        usersDataAccess.CreateUserAsync(new User { Username = name, Email = $"{name}@example.com" });

    private static async Task SetAsync(string connectionString, int userId, string assignments)
    {
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new NpgsqlCommand($"UPDATE users SET {assignments} WHERE id = @id", connection);
        command.Parameters.AddWithValue("@id", userId);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarAsync(string connectionString, int userId, string expression)
    {
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = new NpgsqlCommand($"SELECT {expression} FROM users WHERE id = @id", connection);
        command.Parameters.AddWithValue("@id", userId);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
}
