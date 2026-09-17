namespace RecordService.DataAccess.Email;

/// <summary>
/// Provider-agnostic email sending surface. All provider-specific integration details
/// (auth scheme, request shape, base URL) live behind implementations of this interface,
/// so swapping providers only requires a new implementation plus a Program.cs registration change.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody);
}
