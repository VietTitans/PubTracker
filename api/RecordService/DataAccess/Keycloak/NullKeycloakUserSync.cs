namespace RecordService.DataAccess.Keycloak;

/// <summary>
/// Used when no Keycloak admin client secret is configured: the app keeps working, but an
/// email change is saved in the app's database only.
/// </summary>
public class NullKeycloakUserSync : IKeycloakUserSync
{
    public Task UpdateEmailAsync(string keycloakSub, string newEmail) => Task.CompletedTask;
}
