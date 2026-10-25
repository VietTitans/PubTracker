namespace RecordService.DTOs.UserDto;

/// <summary>
/// Max lengths for user text fields. Shared by DTO validation, the EF column config and
/// Keycloak provisioning so the three never disagree.
/// </summary>
public static class UserFieldLimits
{
    public const int Username = 100;
    public const int Email = 254;
}
