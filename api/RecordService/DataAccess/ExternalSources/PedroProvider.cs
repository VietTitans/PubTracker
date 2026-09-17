using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Playwright;
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

    private static readonly Regex CountRegex = new(@"Found\s+([\d,]+)\s+records", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RecordIdRegex = new(@"record-detail/(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly SemaphoreSlim _browserLock = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public bool CanHandle(string url)
    {
        return !string.IsNullOrWhiteSpace(url) && url.ToLowerInvariant().Contains("pedro");
    }

    public async Task<SourceSearchResult> SearchAsync(string url, DateTime? lastRunDate = null)
    {
        try
        {
            // The search's current total (what the UI displays as "records registered") is
            // refreshed on every poll - it's just a page-1 fetch of the URL as pasted, and is
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
    // alone - see PedroProvider's SearchAsync).
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
        var browser = await GetBrowserAsync();

        await using var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

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
