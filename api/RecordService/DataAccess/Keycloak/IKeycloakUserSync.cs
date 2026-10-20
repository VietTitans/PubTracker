namespace RecordService.DataAccess.Keycloak;

/// <summary>
/// Keeps Keycloak accounts in step with the app's users: email changes and account deletion.
/// </summary>
public interface IKeycloakUserSync
{
    /// <summary>
    /// Changes the email of the Keycloak account with the given <paramref name="keycloakSub"/>
    /// so login and password reset follow the email shown in the app.
    /// </summary>
    /// <exception cref="Exceptions.EmailAlreadyInUseException">Another Keycloak account uses it.</exception>
    Task UpdateEmailAsync(string keycloakSub, string newEmail);

    /// <summary>
    /// Deletes the Keycloak account (it holds the user's email and identity). An account that is
    /// already gone (404) counts as deleted.
    /// </summary>
    Task DeleteUserAsync(string keycloakSub);
}
