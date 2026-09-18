using RecordService.DataAccess.Email;

namespace test;

public class FakeEmailSender : IEmailSender
{
    public List<(string ToEmail, string Subject, string HtmlBody)> SentEmails { get; } = new();

    /// <summary>Addresses to simulate a send failure for, so tests can prove per-user isolation.</summary>
    public HashSet<string> FailForAddresses { get; } = new();

    public Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        if (FailForAddresses.Contains(toEmail))
        {
            throw new InvalidOperationException($"Simulated send failure for {toEmail}");
        }

        SentEmails.Add((toEmail, subject, htmlBody));
        return Task.CompletedTask;
    }
}
