using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RecordService.Authentication;

/// <summary>
/// Development-only auth scheme that fabricates a ClaimTypes.NameIdentifier claim from
/// the X-Debug-User-Id header (defaulting to "1") and a "roles" claim from X-Debug-User-Role
/// (defaulting to "User"), so claims-reading and [Authorize(Policy=...)]-gated endpoints can be
/// exercised via Swagger/Postman before real auth is wired up. The "roles" claim name matches
/// RoleClaimType in Program.cs's ConfigureKeycloakBearer, so RequireRole checks behave the same
/// way here as against a real Keycloak token. Sending X-Debug-Anonymous: true skips
/// authentication entirely, simulating a real anonymous (no-token) request - needed to exercise
/// [AllowAnonymous] endpoints that behave differently for logged-in vs. anonymous callers (e.g.
/// UsersController.CreateUser), since otherwise this handler always authenticates the request.
/// Never registered outside the Development environment - see Program.cs.
/// </summary>
public class DebugAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Debug";
    private const string UserIdHeader = "X-Debug-User-Id";
    private const string UserRoleHeader = "X-Debug-User-Role";
    private const string AnonymousHeader = "X-Debug-Anonymous";
    private const string DefaultUserId = "1";
    private const string DefaultUserRole = "User";

    public DebugAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.TryGetValue(AnonymousHeader, out var anonymousValue) &&
            string.Equals(anonymousValue, "true", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = Request.Headers.TryGetValue(UserIdHeader, out var idValue) && !string.IsNullOrWhiteSpace(idValue)
            ? idValue.ToString()
            : DefaultUserId;

        var role = Request.Headers.TryGetValue(UserRoleHeader, out var roleValue) && !string.IsNullOrWhiteSpace(roleValue)
            ? roleValue.ToString()
            : DefaultUserRole;

        // The 4-arg overload is required here - it's the only one that lets RoleClaimType be set
        // to "roles" instead of the default ClaimTypes.Role, matching the "roles" claim added
        // below and the Keycloak-side RoleClaimType configured in Program.cs's
        // ConfigureKeycloakBearer, so RequireRole checks behave identically under both schemes.
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim("roles", role)
            }, SchemeName, ClaimTypes.NameIdentifier, "roles");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
