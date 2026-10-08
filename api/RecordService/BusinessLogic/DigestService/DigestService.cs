using System.Net;
using System.Text;
using RecordService.DataAccess;
using RecordService.DataAccess.ExternalSources;
using RecordService.Models;

namespace RecordService.BusinessLogic.DigestService;

public class DigestService : IDigestService
{
    private readonly IUsersDataAccess _usersDataAccess;

    public DigestService(IUsersDataAccess usersDataAccess)
    {
        _usersDataAccess = usersDataAccess;
    }

    public async Task<(string Subject, string HtmlBody)?> BuildCombinedDigestAsync(int userId, IReadOnlyList<PendingUserDigest> userQueries)
    {
        var user = await _usersDataAccess.GetUserByIdAsync(userId);
        if (user == null || user.IsMarkedForDeletion)
        {
            return null;
        }

        var totalRecords = userQueries.Sum(q => q.Records.Count);

        var subject = userQueries.Count == 1
            ? $"{totalRecords} new record{(totalRecords == 1 ? "" : "s")} for your search"
            : $"{totalRecords} new record{(totalRecords == 1 ? "" : "s")} across {userQueries.Count} of your searches";

        return (subject, BuildCombinedHtmlBody(userQueries));
    }

    public static string BuildCombinedHtmlBody(IReadOnlyList<PendingUserDigest> pendingQueries)
    {
        var sb = new StringBuilder();
        foreach (var q in pendingQueries)
        {
            var section = SourceDetector.DetectSource(q.TargetUrl) switch
            {
                SourceDetector.SourceType.Pedro => PedroDigestMessageBuilder.BuildHtmlBody(q.TargetUrl, q.Records, q.Summary),
                SourceDetector.SourceType.PubMed => PubMedDigestMessageBuilder.BuildHtmlBody(q.TargetUrl, q.Records, q.Summary),
                _ => BuildHtmlBody(q.TargetUrl, q.Records, q.Summary)
            };
            sb.Append("<div style=\"margin:0 0 32px;\">").Append(section).Append("</div>");
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
