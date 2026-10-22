namespace RecordService.DataAccess.Email;

/// <summary>
/// Provider-agnostic email sending surface. All provider-specific integration details
/// (auth scheme, request shape, base URL) live behind implementations of this interface,
/// so swapping providers only requires a new implementation plus a Program.cs registration change.
/// </summary>
public interface IEmailSender
{
    /// <param name="unsubscribeUrl">When set, sent as List-Unsubscribe / List-Unsubscribe-Post (RFC 8058 one-click) headers.</param>
    Task SendAsync(string toEmail, string subject, string htmlBody, string? unsubscribeUrl = null);
}
