using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RecordService.Authentication;

/// <summary>
/// Development-only auth scheme that fabricates a ClaimTypes.NameIdentifier claim from
/// the X-Debug-User-Id header (defaulting to "1"), so claims-reading endpoints can be
/// exercised via Swagger/Postman before real auth is wired up. Never registered outside
/// the Development environment - see Program.cs.
/// </summary>
public class DebugAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Debug";
    private const string UserIdHeader = "X-Debug-User-Id";
    private const string DefaultUserId = "1";

    public DebugAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers.TryGetValue(UserIdHeader, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : DefaultUserId;

        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
