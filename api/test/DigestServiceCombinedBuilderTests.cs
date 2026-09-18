using RecordService.BusinessLogic.DigestService;
using RecordService.Models;

namespace test;

/// <summary>
/// Pure unit tests of DigestService.BuildCombinedHtmlBody - no DB, no DI, no email sending.
/// Confirms multiple queries/sources combine into one body, a single-query input matches what
/// the direct per-source builder would produce (no behavior change for the single-query
/// case), and an empty input produces an empty body.
/// </summary>
public class DigestServiceCombinedBuilderTests
{
    private static LiteratureRecord MakeRecord(string title, string pmid) => new()
    {
        ExternalId = $"pubmed:{pmid}",
        Title = title,
        SourceUrl = $"https://pubmed.ncbi.nlm.nih.gov/{pmid}/"
    };

    [Fact]
    public void BuildCombinedHtmlBody_TwoSources_CombinesBothIntoOneBody()
    {
        var pubMedRecords = new List<LiteratureRecord> { MakeRecord("A PubMed exercise trial.", "1") };
        var pedroRecords = new List<LiteratureRecord> { MakeRecord("A PEDro exercise trial.", "2") };

        var pending = new List<PendingUserDigest>
        {
            new()
            {
                UserId = 1,
                SearchQueryId = 10,
                TargetUrl = "https://pubmed.ncbi.nlm.nih.gov/?term=exercise",
                Records = pubMedRecords
            },
            new()
            {
                UserId = 1,
                SearchQueryId = 20,
                TargetUrl = "https://search.pedro.org.au/advanced-search/results?body_part=VL01396",
                Records = pedroRecords
            }
        };

        var html = DigestService.BuildCombinedHtmlBody(pending);

        Assert.Contains("A PubMed exercise trial.", html);
        Assert.Contains("A PEDro exercise trial.", html);
    }

    [Fact]
    public void BuildCombinedHtmlBody_SingleQuery_MatchesDirectBuilderCall()
    {
        var records = new List<LiteratureRecord> { MakeRecord("Solo query record.", "1") };
        const string targetUrl = "https://pubmed.ncbi.nlm.nih.gov/?term=solo";

        var pending = new List<PendingUserDigest>
        {
            new() { UserId = 1, SearchQueryId = 10, TargetUrl = targetUrl, Records = records }
        };

        var combinedHtml = DigestService.BuildCombinedHtmlBody(pending);
        var directHtml = PubMedDigestMessageBuilder.BuildHtmlBody(targetUrl, records);

        Assert.Contains(directHtml, combinedHtml);
    }

    [Fact]
    public void BuildCombinedHtmlBody_EmptyList_ReturnsEmptyString()
    {
        var html = DigestService.BuildCombinedHtmlBody(new List<PendingUserDigest>());

        Assert.Equal(string.Empty, html);
    }
}
