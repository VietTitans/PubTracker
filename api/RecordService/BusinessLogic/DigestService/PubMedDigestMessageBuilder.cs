using Microsoft.AspNetCore.WebUtilities;
using RecordService.Models;

namespace RecordService.BusinessLogic.DigestService;

/// <summary>
/// Builds the PubMed digest message body. PubMed has no fixed body-part taxonomy like
/// PEDro's - this does a best-effort keyword match against anatomical MeSH terms found
/// in the search URL's "term" query param, mapped onto the shared DigestCategories so
/// the same categories are used across sources. The first matching keyword wins; a
/// search with no recognizable anatomical term falls back to "your search".
/// </summary>
public static class PubMedDigestMessageBuilder
{
    private static readonly (string Keyword, string Category)[] MeshKeywordMap =
    {
        ("shoulder", DigestCategories.UpperArmShoulderOrShoulderGirdle),
        ("rotator cuff", DigestCategories.UpperArmShoulderOrShoulderGirdle),
        ("humerus", DigestCategories.UpperArmShoulderOrShoulderGirdle),
        ("scapula", DigestCategories.UpperArmShoulderOrShoulderGirdle),
        ("clavicle", DigestCategories.UpperArmShoulderOrShoulderGirdle),
        ("arm", DigestCategories.UpperArmShoulderOrShoulderGirdle),

        ("forearm", DigestCategories.ForearmOrElbow),
        ("elbow", DigestCategories.ForearmOrElbow),
        ("radius", DigestCategories.ForearmOrElbow),
        ("ulna", DigestCategories.ForearmOrElbow),

        ("wrist", DigestCategories.HandOrWrist),
        ("hand", DigestCategories.HandOrWrist),
        ("finger", DigestCategories.HandOrWrist),
        ("thumb", DigestCategories.HandOrWrist),
        ("carpal", DigestCategories.HandOrWrist),

        ("head", DigestCategories.HeadOrNeck),
        ("neck", DigestCategories.HeadOrNeck),
        ("cervical", DigestCategories.HeadOrNeck),
        ("face", DigestCategories.HeadOrNeck),
        ("skull", DigestCategories.HeadOrNeck),
        ("temporomandibular", DigestCategories.HeadOrNeck),

        ("thorax", DigestCategories.Chest),
        ("thoracic wall", DigestCategories.Chest),
        ("chest", DigestCategories.Chest),
        ("rib", DigestCategories.Chest),

        ("thoracic vertebra", DigestCategories.ThoracicSpine),
        ("thoracic spine", DigestCategories.ThoracicSpine),

        ("lumbar", DigestCategories.LumbarSpineSijOrPelvis),
        ("sacroiliac", DigestCategories.LumbarSpineSijOrPelvis),
        ("pelvis", DigestCategories.LumbarSpineSijOrPelvis),
        ("sacrum", DigestCategories.LumbarSpineSijOrPelvis),
        ("low back", DigestCategories.LumbarSpineSijOrPelvis),

        ("perineum", DigestCategories.PerineumOrGenitoUrinarySystem),
        ("urogenital", DigestCategories.PerineumOrGenitoUrinarySystem),
        ("pelvic floor", DigestCategories.PerineumOrGenitoUrinarySystem),

        ("thigh", DigestCategories.ThighOrHip),
        ("hip", DigestCategories.ThighOrHip),
        ("femur", DigestCategories.ThighOrHip),

        ("knee", DigestCategories.LowerLegOrKnee),
        ("leg", DigestCategories.LowerLegOrKnee),
        ("tibia", DigestCategories.LowerLegOrKnee),
        ("fibula", DigestCategories.LowerLegOrKnee),

        ("ankle", DigestCategories.FootOrAnkle),
        ("foot", DigestCategories.FootOrAnkle),
        ("toe", DigestCategories.FootOrAnkle),
    };

    public static string BuildHtmlBody(string targetUrl, IReadOnlyList<LiteratureRecord> newRecords)
    {
        return DigestMessageFormatter.BuildHtmlBody("PubMed", GetCategory(targetUrl), targetUrl, newRecords);
    }

    private static string? GetCategory(string targetUrl)
    {
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var query = QueryHelpers.ParseQuery(uri.Query);
        if (!query.TryGetValue("term", out var term) || string.IsNullOrWhiteSpace(term.ToString()))
        {
            return null;
        }

        var lowerTerm = term.ToString().ToLowerInvariant();
        foreach (var (keyword, category) in MeshKeywordMap)
        {
            if (lowerTerm.Contains(keyword))
            {
                return category;
            }
        }

        return null;
    }
}
