using System.Net;
using System.Text;
using RecordService.DataAccess;
using RecordService.DataAccess.Email;
using RecordService.DataAccess.ExternalSources;
using RecordService.Models;

namespace RecordService.BusinessLogic.DigestService;

public class DigestService : IDigestService
{
    private readonly ISearchQueriesDataAccess _searchQueriesDataAccess;
    private readonly IUsersDataAccess _usersDataAccess;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<DigestService> _logger;

    public DigestService(
        ISearchQueriesDataAccess searchQueriesDataAccess,
        IUsersDataAccess usersDataAccess,
        IEmailSender emailSender,
        ILogger<DigestService> logger)
    {
        _searchQueriesDataAccess = searchQueriesDataAccess;
        _usersDataAccess = usersDataAccess;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<bool> SendDigestForSearchQueryAsync(int searchQueryId, string targetUrl, IReadOnlyList<LiteratureRecord> newRecords)
    {
        if (newRecords.Count == 0)
        {
            return true;
        }

        var subject = $"{newRecords.Count} new record{(newRecords.Count == 1 ? "" : "s")} for your search";
        var htmlBody = SourceDetector.DetectSource(targetUrl) switch
        {
            SourceDetector.SourceType.Pedro => PedroDigestMessageBuilder.BuildHtmlBody(targetUrl, newRecords),
            SourceDetector.SourceType.PubMed => PubMedDigestMessageBuilder.BuildHtmlBody(targetUrl, newRecords),
            _ => BuildHtmlBody(targetUrl, newRecords)
        };

        var subscriberIds = await _searchQueriesDataAccess.GetUserSubscribersForQueryAsync(searchQueryId);
        var allSucceeded = true;

        foreach (var userId in subscriberIds)
        {
            var user = await _usersDataAccess.GetUserByIdAsync(userId);
            if (user == null || user.IsMarkedForDeletion)
            {
                continue;
            }

            try
            {
                await _emailSender.SendAsync(user.Email, subject, htmlBody);
            }
            catch (Exception ex)
            {
                allSucceeded = false;
                _logger.LogWarning(ex,
                    "Failed to send digest for search query {SearchQueryId} to user {UserId}",
                    searchQueryId, userId);
            }
        }

        return allSucceeded;
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
