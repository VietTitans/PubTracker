using System.Net;
using System.Text;
using RecordService.Models;

namespace RecordService.BusinessLogic.DigestService;

/// <summary>
/// Shared HTML formatting for the per-category digest message body, used by every
/// source's message builder (e.g. PedroDigestMessageBuilder, PubMedDigestMessageBuilder)
/// so the layout stays identical across sources - only the category resolution differs.
/// </summary>
internal static class DigestMessageFormatter
{
    public static string BuildHtmlBody(string sourceLabel, string? category, string targetUrl, IReadOnlyList<LiteratureRecord> newRecords)
    {
        var sb = new StringBuilder();
        sb.Append("<p>🧠 ");
        sb.Append(WebUtility.HtmlEncode(sourceLabel));
        sb.Append(" update for: ");
        sb.Append(WebUtility.HtmlEncode(category ?? "your search"));
        sb.Append("<br/>📈 Number of new records: ");
        sb.Append(newRecords.Count);
        sb.Append("<br/>🔗 Search URL: <a href=\"");
        sb.Append(WebUtility.HtmlEncode(targetUrl));
        sb.Append("\">Link</a></p>");

        sb.Append("<ul>");
        foreach (var record in newRecords)
        {
            sb.Append("<li>");

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

            sb.Append("</li>");
        }
        sb.Append("</ul>");

        return sb.ToString();
    }
}
