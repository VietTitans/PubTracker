namespace RecordService.DataAccess.Keycloak;

public interface IKeycloakUserSync
{
    /// <summary>
    /// Changes the email of the Keycloak account with the given <paramref name="keycloakSub"/>
    /// so login and password reset follow the email shown in the app.
    /// </summary>
    /// <exception cref="Exceptions.EmailAlreadyInUseException">Another Keycloak account uses it.</exception>
    Task UpdateEmailAsync(string keycloakSub, string newEmail);
}
