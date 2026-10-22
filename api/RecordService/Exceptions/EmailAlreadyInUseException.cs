namespace RecordService.Exceptions;

/// <summary>
/// Thrown when a user changes their email to one that another Keycloak account already uses.
/// </summary>
public class EmailAlreadyInUseException : Exception
{
    public EmailAlreadyInUseException(string message) : base(message)
    {
    }
}
