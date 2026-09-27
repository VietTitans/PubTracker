using Microsoft.AspNetCore.WebUtilities;
using RecordService.DataAccess.ExternalSources;
using RecordService.Models;

namespace test;

/// <summary>
/// Third test-only ILiteratureSourceProvider, alongside FakeLiteratureSourceProvider and
/// FakePedroLiteratureSourceProvider. Those two always return the same fixed external ids
/// ("fake:1"/"fake:2", "fakepedro:1"/"fakepedro:2") regardless of URL, which is fine for tests
/// with their own factory/Postgres container, but breaks when several test methods share one
/// container (one test class's IClassFixture) - whichever test runs first "claims" those
/// records, and later tests find them already persisted instead of newly-inserted.
///
/// This provider instead derives its two records' external ids from an explicit "recordSet"
/// query parameter, so each test controls isolation directly: a distinct recordSet value per
/// test avoids collisions entirely, while two different search queries deliberately sharing the
/// same recordSet value simulates two different searches surfacing the same underlying record
/// (see AuthorIntentionEndToEndTests' dedup test, which needs exactly that).
/// </summary>
public class FakeUniqueLiteratureSourceProvider : ILiteratureSourceProvider
{
    public const string BaseUrl = "https://example.com/fake-unique-search";

    public string ProviderName => "FakeUnique";

    public bool CanHandle(string url) => url.StartsWith(BaseUrl, StringComparison.Ordinal);

    public Task<SourceSearchResult> SearchAsync(string url, DateTime? lastRunDate = null)
    {
        var recordSet = GetRecordSet(url);

        var allRecords = new List<LiteratureRecord>
        {
            new() { ExternalId = $"fakeunique:{recordSet}:1", Title = $"Fake Unique Record 1 ({recordSet})", Source = ProviderName, DiscoveredAt = DateTime.UtcNow.AddDays(-2) },
            new() { ExternalId = $"fakeunique:{recordSet}:2", Title = $"Fake Unique Record 2 ({recordSet})", Source = ProviderName, DiscoveredAt = DateTime.UtcNow.AddDays(-1) }
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

    private static string GetRecordSet(string url)
    {
        var query = QueryHelpers.ParseQuery(new Uri(url).Query);
        return query.TryGetValue("recordSet", out var value) ? value.ToString() : "default";
    }
}
