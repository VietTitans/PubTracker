namespace RecordService.Exceptions;

/// <summary>
/// Thrown when a user attempts to subscribe to a search query they're already subscribed to.
/// </summary>
public class AlreadySubscribedException : Exception
{
    public AlreadySubscribedException(string message) : base(message)
    {
    }
}
