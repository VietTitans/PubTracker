using RecordService.BusinessLogic.DigestService;
using Xunit;

namespace test;

public class PubMedDigestMessageBuilderTests
{
    [Fact]
    public void GetKeywordTags_NoFilter_ReturnsOnlySearchTag()
    {
        var tags = PubMedDigestMessageBuilder.GetKeywordTags("https://pubmed.ncbi.nlm.nih.gov/?term=covid+vaccine");

        Assert.Equal(new[] { "Search: \"covid vaccine\"" }, tags);
    }

    [Fact]
    public void GetKeywordTags_CustomYearRange_IncludesYearTag()
    {
        var tags = PubMedDigestMessageBuilder.GetKeywordTags(
            "https://pubmed.ncbi.nlm.nih.gov/?term=covid&filter=years.2020-2023");

        Assert.Equal(new[] { "Search: \"covid\"", "Years: 2020-2023" }, tags);
    }

    [Fact]
    public void GetKeywordTags_CustomExactDateRange_IncludesDateTag()
    {
        var tags = PubMedDigestMessageBuilder.GetKeywordTags(
            "https://pubmed.ncbi.nlm.nih.gov/?term=covid&filter=dates.2020%2F1%2F1-2023%2F6%2F30");

        Assert.Equal(new[] { "Search: \"covid\"", "Dates: 2020/1/1 to 2023/6/30" }, tags);
    }

    [Fact]
    public void GetKeywordTags_RelativeYearsFilter_IncludesLastNYearsTag()
    {
        var tags = PubMedDigestMessageBuilder.GetKeywordTags(
            "https://pubmed.ncbi.nlm.nih.gov/?term=covid&filter=datesearch.y_5");

        Assert.Equal(new[] { "Search: \"covid\"", "Last 5 years" }, tags);
    }

    [Fact]
    public void GetKeywordTags_RelativeOneYearFilter_UsesSingularYear()
    {
        var tags = PubMedDigestMessageBuilder.GetKeywordTags(
            "https://pubmed.ncbi.nlm.nih.gov/?term=covid&filter=datesearch.y_1");

        Assert.Equal(new[] { "Search: \"covid\"", "Last 1 year" }, tags);
    }

    [Fact]
    public void GetKeywordTags_YearFilterAlongsideUnrelatedFilter_IgnoresUnrelatedFilter()
    {
        var tags = PubMedDigestMessageBuilder.GetKeywordTags(
            "https://pubmed.ncbi.nlm.nih.gov/?term=covid&filter=years.2020-2023&filter=simsearch2.ffrft");

        Assert.Equal(new[] { "Search: \"covid\"", "Years: 2020-2023" }, tags);
    }
}
