using System.Net;
using System.Text;
using RecordService.DataAccess;
using RecordService.Sources;
using RecordService.Models;

namespace RecordService.BusinessLogic.DigestService;

public class DigestService : IDigestService
{
    private readonly IUsersDataAccess _usersDataAccess;
    private readonly UnsubscribeTokens _unsubscribeTokens;

    public DigestService(IUsersDataAccess usersDataAccess, UnsubscribeTokens unsubscribeTokens)
    {
        _usersDataAccess = usersDataAccess;
        _unsubscribeTokens = unsubscribeTokens;
    }

    public async Task<(string Subject, string HtmlBody)?> BuildCombinedDigestAsync(int userId, IReadOnlyList<PendingUserDigest> userQueries)
    {
        var user = await _usersDataAccess.GetUserByIdAsync(userId);
        if (user == null || user.IsMarkedForDeletion || user.DigestsStopped)
        {
            return null;
        }

        var totalRecords = userQueries.Sum(q => q.Records.Count);

        var subject = userQueries.Count == 1
            ? $"{totalRecords} new record{(totalRecords == 1 ? "" : "s")} for your search"
            : $"{totalRecords} new record{(totalRecords == 1 ? "" : "s")} across {userQueries.Count} of your searches";

        return (subject, BuildCombinedHtmlBody(
            userQueries,
            q => _unsubscribeTokens.SubscriptionUrl(userId, q.SearchQueryId),
            _unsubscribeTokens.StopAllUrl(userId),
            _unsubscribeTokens.PrivacyUrl));
    }

    public static string BuildCombinedHtmlBody(
        IReadOnlyList<PendingUserDigest> pendingQueries,
        Func<PendingUserDigest, string>? subscriptionUnsubscribeUrl = null,
        string? stopAllUrl = null,
        string? privacyUrl = null)
    {
        var sb = new StringBuilder();
        foreach (var q in pendingQueries)
        {
            var section = SourceCatalog.Detect(q.TargetUrl) is { } source
                ? source.BuildDigest(q.TargetUrl, q.Records, q.Summary)
                : BuildHtmlBody(q.TargetUrl, q.Records, q.Summary);
            sb.Append("<div style=\"margin:0 0 32px;\">").Append(section);
            if (subscriptionUnsubscribeUrl != null)
            {
                sb.Append(DigestMessageFormatter.BuildUnsubscribeLine("Unsubscribe from this search", subscriptionUnsubscribeUrl(q)));
            }
            sb.Append("</div>");
        }
        if (stopAllUrl != null && pendingQueries.Count > 0)
        {
            sb.Append(DigestMessageFormatter.BuildUnsubscribeLine("Stop all Digests", stopAllUrl));
        }
        if (privacyUrl != null && pendingQueries.Count > 0)
        {
            sb.Append(DigestMessageFormatter.BuildUnsubscribeLine("Privacy policy", privacyUrl));
        }
        return sb.ToString();
    }

    public static string BuildHtmlBody(string targetUrl, IReadOnlyList<LiteratureRecord> newRecords, string? summary = null)
    {
        var sb = new StringBuilder();
        sb.Append("<p>New records found for your search: ");
        sb.Append(WebUtility.HtmlEncode(targetUrl));
        sb.Append("</p>");

        if (!string.IsNullOrWhiteSpace(summary))
        {
            sb.Append(DigestMessageFormatter.BuildSummaryBlock(summary));
        }

        sb.Append("<ul>");

        foreach (var record in newRecords.Take(DigestMessageFormatter.MaxRecordsPerQuery))
        {
            sb.Append("<li><strong>");

            if (!string.IsNullOrWhiteSpace(record.SourceUrl))
            {
                sb.Append("<a href=\"");
                sb.Append(WebUtility.HtmlEncode(record.SourceUrl));
                sb.Append("\">");
                sb.Append(WebUtility.HtmlEncode(record.Title));
                sb.Append("</a>");
            }
            else
            {
                sb.Append(WebUtility.HtmlEncode(record.Title));
            }

            sb.Append("</strong>");

            if (!string.IsNullOrWhiteSpace(record.Abstract))
            {
                sb.Append("<br/>");
                sb.Append(WebUtility.HtmlEncode(record.Abstract));
            }

            if (!string.IsNullOrWhiteSpace(record.AuthorIntention))
            {
                sb.Append("<br/>Author Intention: ").Append(WebUtility.HtmlEncode(record.AuthorIntention));
            }

            sb.Append("</li>");
        }

        sb.Append("</ul>");
        DigestMessageFormatter.AppendOverflowNotice(sb, newRecords.Count - DigestMessageFormatter.MaxRecordsPerQuery);
        return sb.ToString();
    }
}
