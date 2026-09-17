using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.WebUtilities;
using RecordService.Models;

namespace RecordService.DataAccess.ExternalSources;

/// <summary>
/// PubMed literature source provider. Converts a pasted PubMed search URL into NCBI
/// E-utilities calls: ESearch resolves the search term to PMIDs, EFetch retrieves the
/// full records for those PMIDs.
/// </summary>
public class PubMedProvider : ILiteratureSourceProvider
{
    private const string EutilsBaseUrl = "https://eutils.ncbi.nlm.nih.gov/entrez/eutils/";
    private const int EsearchPageSize = 9999; // NCBI's per-request ceiling for a plain (non-history) ESearch
    private const int EfetchBatchSize = 200; // keep individual EFetch responses to a reasonable size

    public string ProviderName => "PubMed";

    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string? _contactEmail;

    public PubMedProvider(HttpClient httpClient, string? apiKey, string? contactEmail)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _httpClient.BaseAddress = new Uri(EutilsBaseUrl);
        _apiKey = apiKey;
        _contactEmail = contactEmail;
    }

    public bool CanHandle(string url)
    {
        return !string.IsNullOrWhiteSpace(url) && url.ToLowerInvariant().Contains("pubmed");
    }

    public async Task<SourceSearchResult> SearchAsync(string url, DateTime? lastRunDate = null)
    {
        try
        {
            var (totalCount, allRecords) = await FetchRecordsAsync(url);

            var newRecords = lastRunDate.HasValue
                ? allRecords.Where(r => r.DiscoveredAt > lastRunDate.Value).ToList()
                : allRecords;

            return new SourceSearchResult
            {
                Source = ProviderName,
                TotalRecordCount = totalCount,
                NewRecordCount = newRecords.Count,
                NewRecords = newRecords,
                IsSuccessful = true
            };
        }
        catch (Exception ex)
        {
            return new SourceSearchResult
            {
                Source = ProviderName,
                NewRecordCount = 0,
                NewRecords = new(),
                IsSuccessful = false,
                ErrorMessage = $"Error searching PubMed: {ex.Message}"
            };
        }
    }

    public async Task<bool> RefreshAsync(string url)
    {
        try
        {
            await FetchRecordsAsync(url);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<(int TotalCount, List<LiteratureRecord> Records)> FetchRecordsAsync(string url)
    {
        var term = ExtractSearchTerm(url);
        if (string.IsNullOrWhiteSpace(term))
            return (0, new());

        var (totalCount, pmids) = await SearchPmidsAsync(term);
        if (pmids.Count == 0)
            return (totalCount, new());

        return (totalCount, await FetchArticlesAsync(pmids));
    }

    private static string ExtractSearchTerm(string url)
    {
        var uri = new Uri(url);
        var query = QueryHelpers.ParseQuery(uri.Query);
        return query.TryGetValue("term", out var term) ? term.ToString() : string.Empty;
    }

    private async Task<(int TotalCount, List<string> Pmids)> SearchPmidsAsync(string term)
    {
        var pmids = new List<string>();
        var retstart = 0;
        var total = 0;

        while (true)
        {
            var query = BuildQuery(new Dictionary<string, string?>
            {
                ["db"] = "pubmed",
                ["term"] = term,
                ["retstart"] = retstart.ToString(),
                ["retmax"] = EsearchPageSize.ToString(),
                ["retmode"] = "xml"
            });

            var response = await _httpClient.GetAsync($"esearch.fcgi?{query}");
            response.EnsureSuccessStatusCode();

            var xml = XDocument.Parse(await response.Content.ReadAsStringAsync());
            // NCBI's non-history ESearch mode caps out at 9,999 records: a page requested past
            // that ceiling returns an <ERROR> element instead of <Count>, so only overwrite the
            // total when this page actually reports one - otherwise the real total from an
            // earlier page would get clobbered back to 0.
            if (int.TryParse(xml.Root?.Element("Count")?.Value, out var count))
            {
                total = count;
            }
            var page = xml.Root?.Element("IdList")?.Elements("Id").Select(e => e.Value).ToList() ?? new();

            pmids.AddRange(page);
            retstart += EsearchPageSize;

            if (page.Count == 0 || retstart >= total)
                break;

            await Task.Delay(RequestDelay);
        }

        return (total, pmids);
    }

    private async Task<List<LiteratureRecord>> FetchArticlesAsync(List<string> pmids)
    {
        var records = new List<LiteratureRecord>();

        for (var i = 0; i < pmids.Count; i += EfetchBatchSize)
        {
            var batch = pmids.Skip(i).Take(EfetchBatchSize);

            var query = BuildQuery(new Dictionary<string, string?>
            {
                ["db"] = "pubmed",
                ["id"] = string.Join(",", batch),
                ["retmode"] = "xml",
                ["rettype"] = "abstract"
            });

            var response = await _httpClient.GetAsync($"efetch.fcgi?{query}");
            response.EnsureSuccessStatusCode();

            var xml = XDocument.Parse(await response.Content.ReadAsStringAsync());
            records.AddRange(xml.Descendants("PubmedArticle").Select(ParseArticle));
            records.AddRange(xml.Descendants("PubmedBookArticle").Select(ParseBookArticle));

            if (i + EfetchBatchSize < pmids.Count)
                await Task.Delay(RequestDelay);
        }

        return records;
    }

    // NCBI E-utilities usage policy: max 3 requests/sec without an API key, 10/sec with one.
    private TimeSpan RequestDelay => string.IsNullOrEmpty(_apiKey) ? TimeSpan.FromMilliseconds(350) : TimeSpan.FromMilliseconds(110);

    private LiteratureRecord ParseArticle(XElement article)
    {
        var medlineCitation = article.Element("MedlineCitation");
        var pmid = medlineCitation?.Element("PMID")?.Value ?? string.Empty;
        var articleEl = medlineCitation?.Element("Article");

        var doi = article.Element("PubmedData")?.Element("ArticleIdList")?.Elements("ArticleId")
            .FirstOrDefault(e => e.Attribute("IdType")?.Value == "doi")?.Value;

        return BuildRecord(
            pmid,
            title: articleEl?.Element("ArticleTitle")?.Value ?? string.Empty,
            authorElements: articleEl?.Element("AuthorList")?.Elements("Author"),
            abstractParts: articleEl?.Element("Abstract")?.Elements("AbstractText").Select(e => e.Value).ToList(),
            doi: doi,
            pubDate: articleEl?.Element("Journal")?.Element("JournalIssue")?.Element("PubDate"));
    }

    // PubmedBookArticle (book chapters, e.g. StatPearls) has a differently-shaped XML tree
    // than PubmedArticle - title/authors/abstract live directly under BookDocument rather
    // than under Article, and the pub date is under Book instead of Journal/JournalIssue.
    private LiteratureRecord ParseBookArticle(XElement bookArticle)
    {
        var bookDocument = bookArticle.Element("BookDocument");
        var pmid = bookDocument?.Element("PMID")?.Value ?? string.Empty;

        var doi = bookArticle.Element("PubmedBookData")?.Element("ArticleIdList")?.Elements("ArticleId")
            .FirstOrDefault(e => e.Attribute("IdType")?.Value == "doi")?.Value;

        return BuildRecord(
            pmid,
            title: bookDocument?.Element("ArticleTitle")?.Value ?? string.Empty,
            authorElements: bookDocument?.Element("AuthorList")?.Elements("Author"),
            abstractParts: bookDocument?.Element("Abstract")?.Elements("AbstractText").Select(e => e.Value).ToList(),
            doi: doi,
            pubDate: bookDocument?.Element("Book")?.Element("PubDate"));
    }

    private LiteratureRecord BuildRecord(
        string pmid, string title, IEnumerable<XElement>? authorElements, List<string>? abstractParts, string? doi, XElement? pubDate)
    {
        var authors = (authorElements ?? Enumerable.Empty<XElement>())
            .Select(a => string.Join(" ", new[] { a.Element("ForeName")?.Value, a.Element("LastName")?.Value }
                .Where(s => !string.IsNullOrWhiteSpace(s))))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        return new LiteratureRecord
        {
            ExternalId = $"pubmed:{pmid}",
            Doi = string.IsNullOrWhiteSpace(doi) ? null : doi,
            Title = title,
            Authors = string.Join(", ", authors),
            Abstract = abstractParts is { Count: > 0 } ? string.Join(" ", abstractParts) : null,
            PublishedDate = ParsePubDate(pubDate),
            SourceUrl = $"https://pubmed.ncbi.nlm.nih.gov/{pmid}/",
            Source = ProviderName,
            DiscoveredAt = DateTime.UtcNow
        };
    }

    private static DateTime? ParsePubDate(XElement? pubDate)
    {
        if (pubDate is null)
            return null;

        var yearText = pubDate.Element("Year")?.Value;
        if (string.IsNullOrEmpty(yearText))
        {
            // Some records only give a free-text MedlineDate (e.g. "2020 Jan-Feb"); pull the leading year.
            var medlineDate = pubDate.Element("MedlineDate")?.Value;
            var match = medlineDate is null ? null : Regex.Match(medlineDate, @"\d{4}");
            yearText = match is { Success: true } ? match.Value : null;
        }

        if (!int.TryParse(yearText, out var year))
            return null;

        var month = ParseMonth(pubDate.Element("Month")?.Value) ?? 1;
        var day = int.TryParse(pubDate.Element("Day")?.Value, out var d) ? d : 1;

        try
        {
            return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        }
        catch (ArgumentOutOfRangeException)
        {
            return new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }
    }

    private static int? ParseMonth(string? month)
    {
        if (string.IsNullOrWhiteSpace(month))
            return null;

        if (int.TryParse(month, out var numeric))
            return numeric;

        return DateTime.TryParseExact(month, "MMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.Month
            : null;
    }

    private string BuildQuery(Dictionary<string, string?> parameters)
    {
        parameters["tool"] = "PubTracker";
        if (!string.IsNullOrWhiteSpace(_contactEmail))
            parameters["email"] = _contactEmail;
        if (!string.IsNullOrWhiteSpace(_apiKey))
            parameters["api_key"] = _apiKey;

        return string.Join("&", parameters
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value!)}"));
    }
}
