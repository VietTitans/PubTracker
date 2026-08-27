using Microsoft.AspNetCore.WebUtilities;
using RecordService.Models;

namespace RecordService.BusinessLogic.DigestService;

/// <summary>
/// Builds the PEDro digest message body: a body-part category (decoded from the search
/// URL's "body_part" query param via PEDro's own controlled vocabulary) mapped onto the
/// shared DigestCategories, the new-record count, and the search URL embedded as a link.
/// </summary>
public static class PedroDigestMessageBuilder
{
    private static readonly Dictionary<string, string> BodyPartMap = new()
    {
        ["VL01390"] = DigestCategories.HeadOrNeck,
        ["VL01391"] = DigestCategories.UpperArmShoulderOrShoulderGirdle,
        ["VL01392"] = DigestCategories.ForearmOrElbow,
        ["VL01393"] = DigestCategories.HandOrWrist,
        ["VL01394"] = DigestCategories.Chest,
        ["VL01395"] = DigestCategories.ThoracicSpine,
        ["VL01396"] = DigestCategories.LumbarSpineSijOrPelvis,
        ["VL01397"] = DigestCategories.PerineumOrGenitoUrinarySystem,
        ["VL01398"] = DigestCategories.ThighOrHip,
        ["VL01399"] = DigestCategories.LowerLegOrKnee,
        ["VL01400"] = DigestCategories.FootOrAnkle,
        ["VL01401"] = DigestCategories.WholeBodyOrNoSpecificBodyPart
    };

    public static string BuildHtmlBody(string targetUrl, IReadOnlyList<LiteratureRecord> newRecords)
    {
        return DigestMessageFormatter.BuildHtmlBody("PEDro", GetCategory(targetUrl), targetUrl, newRecords);
    }

    private static string? GetCategory(string targetUrl)
    {
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var query = QueryHelpers.ParseQuery(uri.Query);
        if (!query.TryGetValue("body_part", out var bodyPartCode))
        {
            return null;
        }

        return BodyPartMap.TryGetValue(bodyPartCode.ToString(), out var category) ? category : null;
    }
}
