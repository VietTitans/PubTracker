using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Playwright;
using Polly;
using Polly.CircuitBreaker;
using RecordService.Models;

namespace RecordService.DataAccess.ExternalSources;

/// <summary>
/// PEDro (Physiotherapy Evidence Database) literature source provider implementation.
/// Scrapes PEDro advanced-search results pages with a headless browser (PEDro's results
/// table is exposed there, not through any public API).
/// Requires the Playwright Chromium browser to be installed locally:
/// `pwsh bin/Debug/net10.0/playwright.ps1 install chromium` after building.
/// </summary>
public class PedroProvider : ILiteratureSourceProvider, IAsyncDisposable
{
    public string ProviderName => "PEDro";

    // PEDro caps the `perpage` URL param at 1000 server-side regardless of what's requested.
    private const int ScrapePageSize = 1000;

    // The only host this provider is allowed to drive its headless browser to. Without this,
    // any URL merely containing "pedro" (a user-controlled subscription target) would be
    // navigated to directly; an SSRF vector letting a subscriber point the server's browser
    // at internal services/cloud metadata endpoints.
    private const string AllowedHost = "search.pedro.org.au";

    // Bounds how long one scrape can hang the caller (see PageTimeoutMs below), and how long a
    // dead/hanging PEDro can hold RecordPollingService's global poll-cycle lock across many
    // queries before this provider starts short-circuiting instead of retrying every one of
    // them. Same reasoning and thresholds as PubMedHttpClientExtensions.AddPubMedHttpClient -
    // sized for one poll cycle's real call volume, not Polly's much larger HTTP defaults.
    private const int PageTimeoutMs = 15_000;

    private static readonly ResiliencePipeline CircuitBreakerPipeline = new ResiliencePipelineBuilder()
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 1.0,
            MinimumThroughput = 3,
            SamplingDuration = TimeSpan.FromSeconds(PageTimeoutMs / 1000.0 * 3),
            BreakDuration = TimeSpan.FromMinutes(10)
        })
        .Build();

    private static readonly Regex CountRegex = new(@"Found\s+([\d,]+)\s+records", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RecordIdRegex = new(@"record-detail/(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly SemaphoreSlim _browserLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public bool CanHandle(string url) => IsAllowedPedroUrl(url);

    private static bool IsAllowedPedroUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && string.Equals(uri.Host, AllowedHost, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<SourceSearchResult> SearchAsync(string url, DateTime? lastRunDate = null)
    {
        try
        {
            // The search's current total (what the UI displays as "records registered") is
            // refreshed on every poll; it's just a page-1 fetch of the URL as pasted, and is
            // meaningful even when there's nothing new to fetch in detail below.
            var (totalCount, _) = await ScrapeAsync(url);

            // On the very first poll there's no watermark yet, so rather than pulling full
            // details for every matching record (which can be thousands), just record the
            // current total as a baseline. Later polls use PEDro's own
            // "date_record_was_created" filter to fetch only what's actually new since then.
            var newRecords = lastRunDate.HasValue
                ? await ScrapeRecordsAddedSinceAsync(url, lastRunDate.Value)
                : new List<LiteratureRecord>();

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
                ErrorMessage = $"Error searching PEDro: {ex.Message}"
            };
        }
    }

    public async Task<bool> RefreshAsync(string url)
    {
        try
        {
            await ScrapeAsync(url);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Fetches full details for every record PEDro reports as added since `since`, paginating
    // at the server's max page size (records aren't guaranteed to appear on the unfiltered
    // search's first page just because they're new, so this can't be inferred from ScrapeAsync
    // alone, see PedroProvider's SearchAsync).
    private async Task<List<LiteratureRecord>> ScrapeRecordsAddedSinceAsync(string url, DateTime since)
    {
        var dateFilter = since.ToString("dd/MM/yyyy");

        var (totalCount, records) = await ScrapeAsync(WithQueryParams(url, new Dictionary<string, string?>
        {
            ["date_record_was_created"] = dateFilter,
            ["perpage"] = ScrapePageSize.ToString(),
            ["page"] = "1"
        }));

        var totalPages = (int)Math.Ceiling(totalCount / (double)ScrapePageSize);
        for (var page = 2; page <= totalPages; page++)
        {
            var (_, pageRecords) = await ScrapeAsync(WithQueryParams(url, new Dictionary<string, string?>
            {
                ["date_record_was_created"] = dateFilter,
                ["perpage"] = ScrapePageSize.ToString(),
                ["page"] = page.ToString()
            }));
            records.AddRange(pageRecords);
        }

        return records;
    }

    private static string WithQueryParams(string url, Dictionary<string, string?> overrides)
    {
        var uri = new Uri(url);
        var query = QueryHelpers.ParseQuery(uri.Query).ToDictionary(kv => kv.Key, kv => (string?)kv.Value.ToString());

        foreach (var (key, value) in overrides)
        {
            query[key] = value;
        }

        return QueryHelpers.AddQueryString(uri.GetLeftPart(UriPartial.Path), query);
    }

    private async Task<(int RecordCount, List<LiteratureRecord> Records)> ScrapeAsync(string url)
    {
        // Defense-in-depth: CanHandle already gates every entry point via LiteratureSourceFactory,
        // but re-check here too since this drives a real headless browser navigation (SSRF risk).
        // Kept outside CircuitBreakerPipeline below; a rejected URL is a permanent, not a
        // transient, failure and must not count against PEDro's own outage tracking.
        if (!IsAllowedPedroUrl(url))
        {
            throw new InvalidOperationException($"Refusing to navigate to unrecognized PEDro URL: {url}");
        }

        return await CircuitBreakerPipeline.ExecuteAsync(async _ => await ScrapeCoreAsync(url));
    }

    private async Task<(int RecordCount, List<LiteratureRecord> Records)> ScrapeCoreAsync(string url)
    {
        var browser = await GetBrowserAsync();

        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();
        // Applies to every wait/action below (GotoAsync, WaitForSelectorAsync, and the
        // InnerTextAsync/GetAttributeAsync/CountAsync calls further down); a single default
        // rather than a per-call Timeout option on each, since Playwright's own default (30s)
        // is unbounded per call and this method makes several such calls per scrape.
        page.SetDefaultTimeout(PageTimeoutMs);

        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        await page.WaitForSelectorAsync("#search-content");

        var contentText = await page.Locator("#search-content").InnerTextAsync();
        var normalizedText = string.Join(' ', contentText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        var recordCount = 0;
        var countMatch = CountRegex.Match(normalizedText);
        if (countMatch.Success)
        {
            recordCount = int.Parse(countMatch.Groups[1].Value.Replace(",", ""));
        }

        var records = new List<LiteratureRecord>();
        var links = page.Locator("#search-content a[href*='record-detail/']");
        var linkCount = await links.CountAsync();

        for (var i = 0; i < linkCount; i++)
        {
            var link = links.Nth(i);
            var href = await link.GetAttributeAsync("href");
            var title = (await link.InnerTextAsync()).Trim();

            if (string.IsNullOrEmpty(href) || string.IsNullOrEmpty(title))
                continue;

            var idMatch = RecordIdRegex.Match(href);
            if (!idMatch.Success)
                continue;

            records.Add(new LiteratureRecord
            {
                ExternalId = $"pedro:{idMatch.Groups[1].Value}",
                Title = title,
                Authors = string.Empty,
                SourceUrl = href,
                Source = ProviderName,
                DiscoveredAt = DateTime.UtcNow
            });
        }

        return (recordCount, records);
    }

    private async Task<IBrowser> GetBrowserAsync()
    {
        if (_browser is not null)
            return _browser;

        await _browserLock.WaitAsync();
        try
        {
            if (_browser is not null)
                return _browser;

            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            return _browser;
        }
        finally
        {
            _browserLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
            await _browser.CloseAsync();

        _playwright?.Dispose();
        _browserLock.Dispose();
    }
}
