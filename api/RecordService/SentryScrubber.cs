using System.Text.RegularExpressions;
using Sentry;

namespace RecordService;

/// <summary>
/// Removes personal data from an event before it leaves the process: user identity and IP, request
/// bodies, cookies and headers, the signed token in unsubscribe URLs, and any email address that ended
/// up in a message or exception text (for example inside an email provider's error response).
/// </summary>
public static class SentryScrubber
{
    private static readonly Regex Email = new(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);
    private static readonly Regex UnsubscribeToken = new(@"(/unsubscribe/)[^/?#\s]+", RegexOptions.Compiled);

    public static SentryEvent Scrub(SentryEvent sentryEvent)
    {
        sentryEvent.User.Id = null;
        sentryEvent.User.Email = null;
        sentryEvent.User.Username = null;
        sentryEvent.User.IpAddress = null;

        sentryEvent.Request.Data = null;
        sentryEvent.Request.Cookies = null;
        sentryEvent.Request.QueryString = null;
        sentryEvent.Request.Headers.Clear();
        sentryEvent.Request.Env.Clear();
        sentryEvent.Request.Url = Clean(sentryEvent.Request.Url);

        if (sentryEvent.Message is { } message)
        {
            message.Message = Clean(message.Message);
            message.Formatted = Clean(message.Formatted);
            message.Params = null; // the raw values behind the {Placeholders}
        }
        foreach (var exception in sentryEvent.SentryExceptions ?? [])
        {
            exception.Value = Clean(exception.Value);
        }
        return sentryEvent;
    }

    private static string? Clean(string? text) =>
        text == null ? null : UnsubscribeToken.Replace(Email.Replace(text, "[email]"), "$1[token]");
}
