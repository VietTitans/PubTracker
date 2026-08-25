using RecordService.DataAccess.Email;

namespace test;

public class FakeEmailSender : IEmailSender
{
    public List<(string ToEmail, string Subject, string HtmlBody)> SentEmails { get; } = new();

    public Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        SentEmails.Add((toEmail, subject, htmlBody));
        return Task.CompletedTask;
    }
}
