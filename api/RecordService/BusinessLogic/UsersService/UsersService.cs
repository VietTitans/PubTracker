using RecordData;
using RecordService.DataAccess;
using RecordService.DataAccess.Email;
using RecordService.DataAccess.Keycloak;
using RecordService.DTOs.UserDto;

namespace RecordService.BusinessLogic.UsersService;

public class UsersService : IUsersService
{
    private readonly IUsersDataAccess _dataAccess;
    private readonly IKeycloakUserSync _keycloakSync;
    private readonly IEmailSender _emailSender;
    private readonly string _webAppOrigin;
    private const int MaxConsecutivePurgeFailures = 3;

    private readonly ILogger<UsersService> _logger;

    public UsersService(IUsersDataAccess dataAccess, IKeycloakUserSync keycloakSync, IEmailSender emailSender, IConfiguration configuration, ILogger<UsersService> logger)
    {
        _emailSender = emailSender;
        _webAppOrigin = configuration["Cors:WebAppOrigin"] ?? "http://localhost:5173";
        _dataAccess = dataAccess;
        _keycloakSync = keycloakSync;
        _logger = logger;
    }

    public async Task<User> GetUserByIdAsync(int userId)
    {
        return await _dataAccess.GetUserByIdAsync(userId);
    }

    public async Task<Paged<User>> GetUsersPageAsync(int page, int pageSize)
    {
        return await _dataAccess.GetUsersPageAsync(page, pageSize);
    }

    public async Task SetDigestsStoppedAsync(int userId, bool stopped)
    {
        await _dataAccess.SetDigestsStoppedAsync(userId, stopped);
    }

    public async Task<List<SearchQuery>> GetSearchQueriesByUserAsync(int userId)
    {
        return await _dataAccess.GetSearchQueriesByUserAsync(userId);
    }

    public async Task<Paged<SearchQuery>> GetSearchQueriesPageByUserAsync(int userId, int page, int pageSize)
    {
        return await _dataAccess.GetSearchQueriesPageByUserAsync(userId, page, pageSize);
    }

    public async Task<User> CreateUserAsync(User user)
    {
        return await _dataAccess.CreateUserAsync(user);
    }

    public async Task<User> GetOrProvisionByKeycloakSubAsync(string keycloakSub, string email, string username)
    {
        // Token claims bypass DTO validation; clamp to the column limits so login cannot throw.
        return await _dataAccess.GetOrProvisionByKeycloakSubAsync(
            keycloakSub,
            Truncate(email, UserFieldLimits.Email),
            Truncate(username, UserFieldLimits.Username));
    }

    public async Task UpdateUserAsync(int userId, User user, string? keycloakSub = null)
    {
        // Keycloak goes first: if it rejects the change (e.g. the email belongs to another
        // account) the database is left untouched, so the two never disagree.
        if (!string.IsNullOrEmpty(keycloakSub))
        {
            var current = await _dataAccess.GetUserByIdAsync(userId);
            if (current != null && !string.Equals(current.Email, user.Email, StringComparison.OrdinalIgnoreCase))
            {
                await _keycloakSync.UpdateEmailAsync(keycloakSub, user.Email);
            }
        }

        await _dataAccess.UpdateUserAsync(userId, user);
    }

    public async Task SoftDeleteUserAsync(int userId)
    {
        _logger.LogInformation("User {UserId} requested account deletion", userId);
        try
        {
            await _dataAccess.SoftDeleteUserAsync(userId);
            _logger.LogInformation("User {UserId} marked for deletion", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark user {UserId} for deletion", userId);
            throw;
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    public async Task<int> PurgeExpiredDeletedUsersAsync()
    {
        // Per user, inside one database transaction (see PurgeUserAsync): the Keycloak account (email and
        // identity) is deleted first, so a user can never lose their row while their identity remains and
        // a later login silently re-provision them. A failure keeps that user for the next cycle.
        // Several failures in a row mean Keycloak itself is unavailable: stop instead of trying everyone.
        var purgedCount = 0;
        var consecutiveFailures = 0;
        foreach (var userId in await _dataAccess.GetUserIdsDuePurgeAsync())
        {
            try
            {
                var purged = await _dataAccess.PurgeUserAsync(userId, keycloakSub =>
                    string.IsNullOrEmpty(keycloakSub) ? Task.CompletedTask : _keycloakSync.DeleteUserAsync(keycloakSub));
                if (purged)
                {
                    purgedCount++;
                }
                consecutiveFailures = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not purge user {UserId}; keeping the user to retry on the next purge cycle", userId);
                if (++consecutiveFailures >= MaxConsecutivePurgeFailures)
                {
                    _logger.LogError("{Failures} purge failures in a row; stopping this purge cycle", consecutiveFailures);
                    break;
                }
            }
        }

        if (purgedCount > 0)
        {
            _logger.LogInformation("Purged {PurgedCount} user(s) past their deletion grace period", purgedCount);
        }
        return purgedCount;
    }

    public async Task<int> SendInactivityNoticesAsync()
    {
        var sent = 0;
        foreach (var user in await _dataAccess.GetUsersDueInactivityNoticeAsync())
        {
            try
            {
                await _emailSender.SendAsync(user.Email, "Your PubTracker account will be deleted unless you sign in", BuildInactivityNoticeBody(user));
                // Recorded only after a successful send, so a failed send is retried on the next cycle.
                await _dataAccess.MarkInactivityNoticeSentAsync(user.Id);
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send inactivity notice to user {UserId}", user.Id);
            }
        }
        if (sent > 0)
        {
            _logger.LogInformation("Sent {SentCount} inactivity notice(s)", sent);
        }
        return sent;
    }

    private string BuildInactivityNoticeBody(User user) =>
        $"""
         <p>Hello {System.Net.WebUtility.HtmlEncode(user.Username)},</p>
         <p>You have not signed in to PubTracker for 5 years. To protect your privacy we delete inactive accounts:
         your account and all your subscriptions will be permanently deleted in <strong>30 days</strong> unless you sign in before then.</p>
         <p><a href="{_webAppOrigin}">Sign in to keep your account</a></p>
         """;
}
