using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using RecordService.Models;

namespace RecordService.BusinessLogic.DigestService;

/// <summary>
/// Builds the PubMed digest message body. PubMed has no fixed body-part taxonomy like
/// PEDro's - this does a best-effort keyword match against anatomical MeSH terms found
/// in the search URL's "term" query param, mapped onto the shared DigestCategories so
/// the same categories are used across sources. The first matching keyword wins; a
/// search with no recognizable anatomical term falls back to "your search".
/// </summary>
public static class PubMedDigestMessageBuilder
{
    public static string BuildHtmlBody(string targetUrl, IReadOnlyList<LiteratureRecord> newRecords)
    {
        return DigestMessageFormatter.BuildHtmlBody("PubMed", GetCategory(targetUrl), targetUrl, newRecords);
    }

    // PubMed's publication-date sidebar filter shows up in the URL as a "filter" query param
    // (repeatable, alongside other unrelated filters like text availability) in one of three
    // shapes: a custom year range ("years.2020-2023"), a custom exact-date range
    // ("dates.2020/1/1-2023/6/30"), or a quick relative filter ("datesearch.y_5" = last 5 years).
    private static readonly Regex YearsRangeFilter = new(@"^years\.(\d{4})-(\d{4})$", RegexOptions.Compiled);
    private static readonly Regex DatesRangeFilter = new(@"^dates\.([\d/]+)-([\d/]+)$", RegexOptions.Compiled);
    private static readonly Regex RelativeYearsFilter = new(@"^datesearch\.y_(\d+)$", RegexOptions.Compiled);

    /// <summary>
    /// PubMed has no structured fields like PEDro's advanced search - the whole query lives in
    /// the "term" param, which can be an arbitrary boolean expression. So unlike PEDro's
    /// multi-tag breakdown, this returns at most a "Search: ..." tag with the raw term, plus a
    /// year-filter tag if the URL's publication-date filter is present.
    /// </summary>
    public static List<string> GetKeywordTags(string targetUrl)
    {
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            return new();
        }

        var tags = new List<string>();
        var query = QueryHelpers.ParseQuery(uri.Query);

        if (query.TryGetValue("term", out var term) && !string.IsNullOrWhiteSpace(term.ToString()))
        {
            tags.Add($"Search: \"{term}\"");
        }

        var yearFilterTag = GetYearFilterTag(query);
        if (yearFilterTag is not null)
        {
            tags.Add(yearFilterTag);
        }

        return tags;
    }

    private static string? GetYearFilterTag(Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query)
    {
        if (!query.TryGetValue("filter", out var filters))
        {
            return null;
        }

        foreach (var filter in filters)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                continue;
            }

            var yearsMatch = YearsRangeFilter.Match(filter);
            if (yearsMatch.Success)
            {
                return $"Years: {yearsMatch.Groups[1].Value}-{yearsMatch.Groups[2].Value}";
            }

            var datesMatch = DatesRangeFilter.Match(filter);
            if (datesMatch.Success)
            {
                return $"Dates: {datesMatch.Groups[1].Value} to {datesMatch.Groups[2].Value}";
            }

            var relativeMatch = RelativeYearsFilter.Match(filter);
            if (relativeMatch.Success)
            {
                var years = relativeMatch.Groups[1].Value;
                return $"Last {years} year{(years == "1" ? "" : "s")}";
            }
        }

        return null;
    }

    private static string? GetCategory(string targetUrl)
    {
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var query = QueryHelpers.ParseQuery(uri.Query);
        return query.TryGetValue("term", out var term) ? CategoryKeywordMatcher.Match(term.ToString()) : null;
    }
}
