using RecordService.BusinessLogic.DigestService;
using Xunit;

namespace test;

public class PedroDigestMessageBuilderTests
{
    [Fact]
    public void GetCategory_StructuredBodyPartSet_UsesBodyPartLabelDirectly()
    {
        var category = PedroDigestMessageBuilder.GetCategory(
            "https://search.pedro.org.au/advanced-search/results?body_part=VL01396&abstract_with_title=");

        Assert.Equal("Lumbar spine, SIJ or pelvis", category);
    }

    [Fact]
    public void GetCategory_BodyPartLeftAtAnyAll_FallsBackToKeywordMatchOnFreeTextSearch()
    {
        // Matches the real shape of a PEDro "Any/all" body_part search: body_part=0 with a
        // free-text term is what used to render as "your search" instead of a real category.
        var category = PedroDigestMessageBuilder.GetCategory(
            "https://search.pedro.org.au/advanced-search/results?abstract_with_title=wrist&body_part=0");

        Assert.Equal("Hand or wrist", category);
    }

    [Fact]
    public void GetCategory_NoBodyPartAndNoMatchableFreeText_ReturnsNull()
    {
        var category = PedroDigestMessageBuilder.GetCategory(
            "https://search.pedro.org.au/advanced-search/results?abstract_with_title=&body_part=0");

        Assert.Null(category);
    }
}
