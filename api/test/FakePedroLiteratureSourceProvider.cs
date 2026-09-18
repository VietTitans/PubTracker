using RecordService.DataAccess.ExternalSources;
using RecordService.Models;

namespace test;

/// <summary>
/// Second test-only ILiteratureSourceProvider stand-in, alongside FakeLiteratureSourceProvider,
/// so E2E tests can exercise a user subscribed to two distinct sources at once without hitting
/// live PubMed/PEDro. CanHandle matches a URL containing "pedro" so SourceDetector routes its
/// digest content through PedroDigestMessageBuilder, distinguishing it from
/// FakeLiteratureSourceProvider's Unknown-source fallback path.
/// </summary>
public class FakePedroLiteratureSourceProvider : ILiteratureSourceProvider
{
    public const string TestUrl = "https://example.com/pedro/fake-search";

    public string ProviderName => "FakePedro";

    public bool CanHandle(string url) => url == TestUrl;

    public Task<SourceSearchResult> SearchAsync(string url, DateTime? lastRunDate = null)
    {
        var allRecords = new List<LiteratureRecord>
        {
            new() { ExternalId = "fakepedro:1", Title = "Fake Pedro Record 1", Source = ProviderName, DiscoveredAt = DateTime.UtcNow.AddDays(-2) },
            new() { ExternalId = "fakepedro:2", Title = "Fake Pedro Record 2", Source = ProviderName, DiscoveredAt = DateTime.UtcNow.AddDays(-1) }
        };

        var newRecords = lastRunDate.HasValue
            ? allRecords.Where(r => r.DiscoveredAt > lastRunDate.Value).ToList()
            : allRecords;

        return Task.FromResult(new SourceSearchResult
        {
            Source = ProviderName,
            NewRecordCount = newRecords.Count,
            NewRecords = newRecords,
            IsSuccessful = true
        });
    }

    public Task<bool> RefreshAsync(string url) => Task.FromResult(true);
}
