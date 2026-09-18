using System.Net;
using System.Text;
using RecordService.Models;

namespace RecordService.BusinessLogic.DigestService;

/// <summary>
/// Shared HTML formatting for the per-category digest message body, used by every
/// source's message builder (e.g. PedroDigestMessageBuilder, PubMedDigestMessageBuilder)
/// so the layout stays identical across sources - only the category resolution differs.
/// Records are grouped by RecordTopicClassifier instead of listed flat; every record is
/// still listed in full under its cluster, nothing is capped or linked out.
/// Styling is inline on every element rather than a &lt;style&gt; block or classes, since
/// most email clients (Gmail included) strip both - inline is the only styling that
/// reliably survives into an inbox.
/// </summary>
internal static class DigestMessageFormatter
{
    private const string FontFamily = "Arial, Helvetica, sans-serif";
    private const string InkColor = "#1b221f";
    private const string BorderColor = "#dfe3de";
    private const string DividerColor = "#eceee9";
    private const string AccentColor = "#3b6e5e";
    private const string OtherBorderColor = "#a6592e";
    private const string OtherBackgroundColor = "#f1e2d6";

    public static string BuildHtmlBody(string sourceLabel, string? category, string targetUrl, IReadOnlyList<LiteratureRecord> newRecords)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:").Append(FontFamily).Append(";max-width:640px;margin:0 auto;color:").Append(InkColor).Append(";\">");

        sb.Append("<p style=\"margin:0 0 20px;padding:14px 18px;background:#f1f3ef;border-radius:8px;font-size:14px;line-height:1.6;\">");
        sb.Append("🧠 ");
        sb.Append(WebUtility.HtmlEncode(sourceLabel));
        sb.Append(" update for: <strong>");
        sb.Append(WebUtility.HtmlEncode(category ?? "your search"));
        sb.Append("</strong><br/>📈 Number of new records: <strong>");
        sb.Append(newRecords.Count);
        sb.Append("</strong><br/>🔗 Search URL: <a href=\"");
        sb.Append(WebUtility.HtmlEncode(targetUrl));
        sb.Append("\" style=\"color:").Append(AccentColor).Append(";\">Link</a></p>");

        var byCluster = newRecords.ToLookup(RecordTopicClassifier.Classify);

        foreach (var clusterName in RecordTopicClassifier.DisplayOrder)
        {
            var records = byCluster[clusterName].ToList();
            if (records.Count > 0)
            {
                AppendCluster(sb, clusterName, records, isOther: false);
            }
        }

        var uncategorized = byCluster[null].ToList();
        if (uncategorized.Count > 0)
        {
            AppendCluster(sb, "Other / uncategorized", uncategorized, isOther: true);
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    private static void AppendCluster(StringBuilder sb, string clusterName, List<LiteratureRecord> records, bool isOther)
    {
        var borderColor = isOther ? OtherBorderColor : BorderColor;
        var background = isOther ? OtherBackgroundColor : "#ffffff";

        sb.Append("<div style=\"margin:0 0 16px;padding:14px 18px;border:1px solid ").Append(borderColor)
          .Append(";border-radius:8px;background:").Append(background).Append(";\">");

        sb.Append("<h4 style=\"margin:0 0 10px;font-size:15px;font-weight:600;color:").Append(InkColor).Append(";\">")
          .Append(WebUtility.HtmlEncode(clusterName)).Append(" (").Append(records.Count).Append(")</h4>");

        sb.Append("<ul style=\"margin:0;padding:0;list-style:none;\">");
        for (var i = 0; i < records.Count; i++)
        {
            var isLast = i == records.Count - 1;
            sb.Append("<li style=\"padding:8px 0;font-size:14px;line-height:1.5;");
            if (!isLast)
            {
                sb.Append("border-bottom:1px solid ").Append(DividerColor).Append(';');
            }
            sb.Append("\">");
            // Manually numbered rather than an <ol> - Outlook's HTML rendering engine is
            // notorious for dropping or mis-numbering <ol>/<li> counters, so plain text is the
            // only numbering that reliably survives across email clients (same reasoning as
            // inlining every style rather than relying on a <style> block).
            sb.Append("<span style=\"display:inline-block;min-width:26px;color:#8b948d;\">")
              .Append(i + 1).Append(".</span>");
            AppendRecordLink(sb, records[i]);
            sb.Append("</li>");
        }
        sb.Append("</ul>");

        sb.Append("</div>");
    }

    private static void AppendRecordLink(StringBuilder sb, LiteratureRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.SourceUrl))
        {
            sb.Append("<a href=\"");
            sb.Append(WebUtility.HtmlEncode(record.SourceUrl));
            sb.Append("\" style=\"color:").Append(InkColor).Append(";text-decoration:none;border-bottom:1px solid ").Append(BorderColor).Append(";\">");
            sb.Append(WebUtility.HtmlEncode(record.Title));
            sb.Append("</a>");
        }
        else
        {
            sb.Append(WebUtility.HtmlEncode(record.Title));
        }
    }
}
