using RecordService.DataAccess.ExternalSources;

namespace test;

/// <summary>
/// Manual, opt-in verification that PubMedProvider's E-utilities calls still match NCBI's
/// live API shape. Skipped by default (real network, not for CI). To run it: remove the
/// Skip attribute below, run this test alone, then restore Skip before committing.
/// </summary>
public class PubMedProviderManualTest
{
    private const string TestUrl = "https://pubmed.ncbi.nlm.nih.gov/?term=covid+vaccine";

    [Fact(Skip = "Manual only - hits the live NCBI E-utilities API. Remove Skip locally to run.")]
    public async Task SearchAsync_ParsesRealPubMedResults()
    {
        var provider = new PubMedProvider(new HttpClient(), apiKey: null, contactEmail: null);

        var result = await provider.SearchAsync(TestUrl);

        Assert.True(result.IsSuccessful, result.ErrorMessage);
        Assert.NotEmpty(result.NewRecords);

        foreach (var record in result.NewRecords.Take(5))
        {
            Console.WriteLine($"{record.Doi} | {record.Title} | {record.SourceUrl}");
        }
    }
}
