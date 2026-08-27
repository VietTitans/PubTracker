using Microsoft.AspNetCore.WebUtilities;
using RecordService.Models;

namespace RecordService.BusinessLogic.DigestService;

/// <summary>
/// Decodes PEDro's advanced-search query params (body_part, therapy, problem, topic) into
/// human-readable labels, and builds the PEDro digest message body. Every map below was
/// scraped directly from search.pedro.org.au/advanced-search's own <select> options - PEDro
/// exposes no API for these, so the option lists are copied here rather than re-fetched at
/// runtime. Re-scrape and update by hand if PEDro's form options ever change.
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

    private static readonly Dictionary<string, string> TherapyMap = new()
    {
        ["VL01376"] = "Acupuncture",
        ["VL01377"] = "Behaviour modification",
        ["VL01378"] = "Education",
        ["VL01379"] = "Electrotherapies, heat, cold",
        ["VL01380"] = "Fitness training",
        ["VL01381"] = "Health promotion",
        ["VL01382"] = "Hydrotherapy, balneotherapy",
        ["VL01383"] = "Neurodevelopmental therapy, neurofacilitation",
        ["VL01384"] = "Orthoses, taping, splinting",
        ["VL01385"] = "Respiratory therapy",
        ["VL01386"] = "Skill training",
        ["VL01387"] = "Strength training",
        ["VL01388"] = "Stretching, mobilisation, manipulation, massage"
    };

    private static readonly Dictionary<string, string> ProblemMap = new()
    {
        ["VL01363"] = "Difficulty with sputum clearance",
        ["VL01364"] = "Frailty",
        ["VL01365"] = "Impaired ventilation",
        ["VL01366"] = "Incontinence",
        ["VL01367"] = "Motor incoordination",
        ["VL01368"] = "Muscle shortening, reduced joint compliance",
        ["VL01369"] = "Muscle weakness",
        ["VL01370"] = "Oedema",
        ["VL01371"] = "Pain",
        ["VL01372"] = "Reduced exercise tolerance",
        ["VL01373"] = "Reduced work tolerance",
        ["VL01374"] = "Skin lesion, wound, burn"
    };

    private static readonly Dictionary<string, string> TopicMap = new()
    {
        ["VL01402"] = "Chronic pain",
        ["VL01403"] = "Chronic respiratory disease",
        ["VL01404"] = "Neurotrauma",
        ["VL01405"] = "Whiplash",
        ["VL01407"] = "Cerebral palsy"
    };

    public static string BuildHtmlBody(string targetUrl, IReadOnlyList<LiteratureRecord> newRecords)
    {
        return DigestMessageFormatter.BuildHtmlBody("PEDro", GetBodyPartLabel(targetUrl), targetUrl, newRecords);
    }

    public static string? GetBodyPartLabel(string targetUrl) => GetMappedValue(targetUrl, "body_part", BodyPartMap);

    public static string? GetTherapyLabel(string targetUrl) => GetMappedValue(targetUrl, "therapy", TherapyMap);

    public static string? GetProblemLabel(string targetUrl) => GetMappedValue(targetUrl, "problem", ProblemMap);

    public static string? GetTopicLabel(string targetUrl) => GetMappedValue(targetUrl, "topic", TopicMap);

    /// <summary>Raw passthrough, not a coded value - PEDro's year_of_publication field is free text.</summary>
    public static string? GetPublicationYear(string targetUrl) => GetRawQueryValue(targetUrl, "year_of_publication");

    private static string? GetMappedValue(string targetUrl, string paramName, Dictionary<string, string> map)
    {
        var code = GetRawQueryValue(targetUrl, paramName);
        return code is not null && map.TryGetValue(code, out var label) ? label : null;
    }

    private static string? GetRawQueryValue(string targetUrl, string paramName)
    {
        if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var query = QueryHelpers.ParseQuery(uri.Query);
        if (!query.TryGetValue(paramName, out var value) || string.IsNullOrEmpty(value))
        {
            return null;
        }

        return value.ToString();
    }
}
