using System.Security.Claims;
using RecordService.Exceptions;

namespace RecordService.Authentication;

/// <summary>
/// Who is calling a service method. User-scoped and admin-only service methods take one and enforce the
/// rules themselves, so a caller that did not come through a controller (hook, other service) is checked too.
/// Deny by default: a missing caller (null) throws, and so does one without a real user id.
/// </summary>
public sealed record Caller(int UserId, bool IsAdmin)
{
    /// <summary>Build from the authenticated principal. The controller has already rejected a missing id claim.</summary>
    public static Caller From(ClaimsPrincipal user) =>
        new(int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), user.IsInRole("Admin"));

    /// <summary>Only the user themselves, not even an admin.</summary>
    public void RequireSelf(int userId)
    {
        if (UserId <= 0 || UserId != userId) throw new ForbiddenException();
    }

    public void RequireSelfOrAdmin(int userId)
    {
        if (UserId <= 0 || (!IsAdmin && UserId != userId)) throw new ForbiddenException();
    }

    public void RequireAdmin()
    {
        if (UserId <= 0 || !IsAdmin) throw new ForbiddenException();
    }
}
