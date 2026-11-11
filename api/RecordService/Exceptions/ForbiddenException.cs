namespace RecordService.Exceptions;

/// <summary>
/// Thrown by a service when its <see cref="Authentication.Caller"/> may not perform the operation.
/// Controllers answer 403.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException() : base("Caller is not allowed to perform this operation.")
    {
    }
}
