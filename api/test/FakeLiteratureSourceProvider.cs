using RecordService.DataAccess.ExternalSources;
using RecordService.Models;

namespace test;

/// <summary>
/// Test-only ILiteratureSourceProvider stand-in for the E2E tests, avoiding any dependency
/// on live PubMed/PEDro network access. Returns two canned records with fixed DiscoveredAt
/// timestamps so tests can assert delta-fetch (lastRunDate) filtering behavior.
/// </summary>
public class FakeLiteratureSourceProvider : ILiteratureSourceProvider
{
    public const string TestUrl = "https://example.com/fake-search";

    public string ProviderName => "Fake";

    // Prefix match (not exact) so tests needing their own distinct, non-colliding query URL -
    // e.g. two tests in the same class sharing one Postgres container/class fixture - can mint
    // variants like $"{TestUrl}?case=isolation" that still resolve to this same fake provider.
    public bool CanHandle(string url) => url.StartsWith(TestUrl, StringComparison.Ordinal);

    public Task<SourceSearchResult> SearchAsync(string url, DateTime? lastRunDate = null)
    {
        var allRecords = new List<LiteratureRecord>
        {
            new() { ExternalId = "fake:1", Title = "Fake Record 1", Source = ProviderName, DiscoveredAt = DateTime.UtcNow.AddDays(-2) },
            new() { ExternalId = "fake:2", Title = "Fake Record 2", Source = ProviderName, DiscoveredAt = DateTime.UtcNow.AddDays(-1) }
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
