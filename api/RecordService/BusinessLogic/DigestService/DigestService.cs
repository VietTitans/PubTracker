using System.Net;
using System.Text;
using RecordService.DataAccess;
using RecordService.DataAccess.Email;
using RecordService.DataAccess.ExternalSources;
using RecordService.DataAccess.Summarization;
using RecordService.Models;

namespace RecordService.BusinessLogic.DigestService;

public class DigestService : IDigestService
{
    private readonly IUsersDataAccess _usersDataAccess;
    private readonly IEmailSender _emailSender;
    private readonly ISummaryGenerator _summaryGenerator;
    private readonly ILogger<DigestService> _logger;

    public DigestService(
        IUsersDataAccess usersDataAccess,
        IEmailSender emailSender,
        ISummaryGenerator summaryGenerator,
        ILogger<DigestService> logger)
    {
        _usersDataAccess = usersDataAccess;
        _emailSender = emailSender;
        _summaryGenerator = summaryGenerator;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<(int UserId, int SearchQueryId), bool>> SendCombinedDigestsAsync(IReadOnlyList<PendingUserDigest> pendingDigests)
    {
        var successByKey = pendingDigests.ToDictionary(p => (p.UserId, p.SearchQueryId), _ => true);
        if (pendingDigests.Count == 0)
        {
            return successByKey;
        }

        foreach (var group in pendingDigests.GroupBy(p => p.UserId))
        {
            var userId = group.Key;
            var userQueries = group.ToList();

            var user = await _usersDataAccess.GetUserByIdAsync(userId);
            if (user == null || user.IsMarkedForDeletion)
            {
                continue; // not a failure - no one to send to
            }

            var summary = await GenerateSummaryAsync(userId, userQueries);
            var totalRecords = userQueries.Sum(q => q.Records.Count);

            var htmlBody = BuildRecordCountLine(totalRecords);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                htmlBody += BuildSummaryBlock(summary);
            }
            htmlBody += BuildCombinedHtmlBody(userQueries);

            var subject = userQueries.Count == 1
                ? $"{totalRecords} new record{(totalRecords == 1 ? "" : "s")} for your search"
                : $"{totalRecords} new record{(totalRecords == 1 ? "" : "s")} across {userQueries.Count} of your searches";

            try
            {
                await _emailSender.SendAsync(user.Email, subject, htmlBody);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to send combined digest to user {UserId} ({QueryCount} quer(ies))",
                    userId, userQueries.Count);
                foreach (var q in userQueries)
                {
                    successByKey[(userId, q.SearchQueryId)] = false;
                }
            }
        }

        return successByKey;
    }

    private async Task<string> GenerateSummaryAsync(int userId, IReadOnlyList<PendingUserDigest> userQueries)
    {
        try
        {
            var allRecords = userQueries.SelectMany(q => q.Records).ToList();
            return await _summaryGenerator.SummarizeAsync(allRecords);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate digest summary for user {UserId}", userId);
            return string.Empty;
        }
    }

    private static string BuildRecordCountLine(int totalRecords) =>
        $"<p style=\"margin:0 0 16px;font-size:14px;\">📈 <strong>{totalRecords} new record{(totalRecords == 1 ? "" : "s")} found</strong></p>";

    private static string BuildSummaryBlock(string summary) =>
        "<div style=\"margin:0 0 24px;padding:14px 18px;background:#eef3f0;border-radius:8px;font-size:14px;line-height:1.6;\">"
        + "<h4 style=\"margin:0 0 8px;font-size:15px;font-weight:600;\">🧠 AI Summary</h4>"
        + "<p style=\"margin:0;\">" + WebUtility.HtmlEncode(summary) + "</p>"
        + "</div>";

    public static string BuildCombinedHtmlBody(IReadOnlyList<PendingUserDigest> pendingQueries)
    {
        var sb = new StringBuilder();
        foreach (var q in pendingQueries)
        {
            var section = SourceDetector.DetectSource(q.TargetUrl) switch
            {
                SourceDetector.SourceType.Pedro => PedroDigestMessageBuilder.BuildHtmlBody(q.TargetUrl, q.Records),
                SourceDetector.SourceType.PubMed => PubMedDigestMessageBuilder.BuildHtmlBody(q.TargetUrl, q.Records),
                _ => BuildHtmlBody(q.TargetUrl, q.Records)
            };
            sb.Append("<div style=\"margin:0 0 32px;\">").Append(section).Append("</div>");
        }
        return sb.ToString();
    }

    public static string BuildHtmlBody(string targetUrl, IReadOnlyList<LiteratureRecord> newRecords)
    {
        var sb = new StringBuilder();
        sb.Append("<p>New records found for your search: ");
        sb.Append(WebUtility.HtmlEncode(targetUrl));
        sb.Append("</p><ul>");

        foreach (var record in newRecords)
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

            sb.Append("</li>");
        }

        sb.Append("</ul>");
        return sb.ToString();
    }
}
