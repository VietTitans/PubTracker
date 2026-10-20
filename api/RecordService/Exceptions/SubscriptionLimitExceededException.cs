namespace RecordService.Exceptions;

/// <summary>
/// Thrown when subscribing would take a user past <c>Subscriptions:MaxPerUser</c>.
/// </summary>
public class SubscriptionLimitExceededException : Exception
{
    public int Limit { get; }

    public SubscriptionLimitExceededException(int limit) : base($"You can have at most {limit} subscriptions.")
    {
        Limit = limit;
    }
}
