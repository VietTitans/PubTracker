using RecordService.BusinessLogic.DigestService;
using RecordService.Models;

namespace test;

/// <summary>
/// Covers DigestMessageFormatter's clustering behavior (via PubMedDigestMessageBuilder's
/// public entry point, since DigestMessageFormatter itself is internal): records are grouped
/// by RecordTopicClassifier under a heading with a count, every record in a cluster is listed
/// in full (nothing capped or linked out), and anything no rule matches lands in its own
/// "Other / uncategorized" cluster, also listed in full.
/// </summary>
public class DigestMessageClusteringTests
{
    private const string TestUrl = "https://pubmed.ncbi.nlm.nih.gov/?term=chronic+low+back+pain";

    private static LiteratureRecord MakeRecord(string title, string pmid) => new()
    {
        ExternalId = $"pubmed:{pmid}",
        Title = title,
        SourceUrl = $"https://pubmed.ncbi.nlm.nih.gov/{pmid}/"
    };

    [Fact]
    public void BuildHtmlBody_GroupsRecordsUnderMatchingClusterHeading()
    {
        var records = new List<LiteratureRecord>
        {
            MakeRecord("Yoga for chronic low back pain: a systematic review and meta-analysis.", "1"),
            MakeRecord("Effectiveness of exercise therapy for chronic low back pain.", "2"),
        };

        var html = PubMedDigestMessageBuilder.BuildHtmlBody(TestUrl, records);

        Assert.Contains("Systematic reviews &amp; meta-analyses (1)", html);
        Assert.Contains("Exercise &amp; movement-based therapy (1)", html);
    }

    [Fact]
    public void BuildHtmlBody_LargeCluster_ListsEveryRecordWithoutCappingOrLinkingOut()
    {
        var records = Enumerable.Range(1, 5)
            .Select(i => MakeRecord($"Study protocol for a randomized controlled trial #{i}.", i.ToString()))
            .ToList();

        var html = PubMedDigestMessageBuilder.BuildHtmlBody(TestUrl, records);

        Assert.Contains("RCT protocols (5)", html);
        for (var i = 1; i <= 5; i++)
        {
            Assert.Contains($"#{i}.</a>", html);
        }
        Assert.DoesNotContain("more", html);
    }

    [Fact]
    public void BuildHtmlBody_UnmatchedTitles_GoToOtherClusterListedInFull()
    {
        var records = new List<LiteratureRecord>
        {
            MakeRecord("A completely unrelated title about nothing classifiable.", "9"),
        };

        var html = PubMedDigestMessageBuilder.BuildHtmlBody(TestUrl, records);

        Assert.Contains("Other / uncategorized (1)", html);
        Assert.Contains("A completely unrelated title about nothing classifiable.", html);
    }

    [Fact]
    public void BuildHtmlBody_TitleMatchingTwoRules_UsesFirstClusterInDisplayOrder()
    {
        // Matches both "systematic review" (earlier in DisplayOrder) and "exercise" (later).
        var records = new List<LiteratureRecord>
        {
            MakeRecord("Exercise interventions for low back pain: a systematic review.", "1"),
        };

        var html = PubMedDigestMessageBuilder.BuildHtmlBody(TestUrl, records);

        Assert.Contains("Systematic reviews &amp; meta-analyses (1)", html);
        Assert.DoesNotContain("Exercise &amp; movement-based therapy", html);
    }
}
