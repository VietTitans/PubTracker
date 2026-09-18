using Microsoft.AspNetCore.WebUtilities;
using RecordService.Models;

namespace RecordService.BusinessLogic.DigestService;

/// <summary>
/// Decodes PEDro's advanced-search query params into human-readable labels/tags, and builds
/// the PEDro digest message body. The coded-field maps below were scraped directly from
/// search.pedro.org.au/advanced-search's own &lt;select&gt; options - PEDro exposes no API for
/// these, so the option lists are copied here rather than re-fetched at runtime. Re-scrape
/// and update by hand if PEDro's form options ever change.
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

    private static readonly Dictionary<string, string> SubdisciplineMap = new()
    {
        ["VL01352"] = "Cardiothoracics",
        ["VL01353"] = "Continence and women's health",
        ["VL01354"] = "Ergonomics and occupational health",
        ["VL01355"] = "Gerontology",
        ["VL01356"] = "Musculoskeletal",
        ["VL01357"] = "Neurology",
        ["VL01358"] = "Oncology",
        ["VL01359"] = "Orthopaedics",
        ["VL01360"] = "Paediatrics",
        ["VL01361"] = "Sports"
    };

    // Unlike the VL-coded fields above, PEDro's "method" field uses its own literal values
    // as the URL parameter (e.g. method=systematic+review) rather than a VL code.
    private static readonly Dictionary<string, string> MethodMap = new()
    {
        ["practice guideline"] = "Practice guideline",
        ["systematic review"] = "Systematic review",
        ["clinical trial"] = "Clinical trial"
    };

    public static string BuildHtmlBody(string targetUrl, IReadOnlyList<LiteratureRecord> newRecords)
    {
        return DigestMessageFormatter.BuildHtmlBody("PEDro", GetCategory(targetUrl), targetUrl, newRecords);
    }

    /// <summary>
    /// PEDro's structured body_part field wins when the search actually set one; a search left
    /// at "Any/all" (body_part=0, or the param missing entirely) has no structured field to read,
    /// so this falls back to keyword-matching the free-text search fields against the same
    /// taxonomy PubMed uses (CategoryKeywordMatcher) - otherwise those searches would always
    /// show "your search" instead of a real category.
    /// </summary>
    public static string? GetCategory(string targetUrl) =>
        GetBodyPartLabel(targetUrl)
        ?? CategoryKeywordMatcher.Match(GetRawQueryValue(targetUrl, "abstract_with_title"))
        ?? CategoryKeywordMatcher.Match(GetRawQueryValue(targetUrl, "title"))
        ?? CategoryKeywordMatcher.Match(GetRawQueryValue(targetUrl, "calc_text"));

    public static string? GetBodyPartLabel(string targetUrl) => GetMappedValue(targetUrl, "body_part", BodyPartMap);

    public static string? GetTherapyLabel(string targetUrl) => GetMappedValue(targetUrl, "therapy", TherapyMap);

    public static string? GetProblemLabel(string targetUrl) => GetMappedValue(targetUrl, "problem", ProblemMap);

    public static string? GetTopicLabel(string targetUrl) => GetMappedValue(targetUrl, "topic", TopicMap);

    public static string? GetSubdisciplineLabel(string targetUrl) => GetMappedValue(targetUrl, "subdiscipline", SubdisciplineMap);

    public static string? GetMethodLabel(string targetUrl) => GetMappedValue(targetUrl, "method", MethodMap);

    /// <summary>Raw passthrough, not a coded value - PEDro's year_of_publication field is free text.</summary>
    public static string? GetPublicationYear(string targetUrl) => GetRawQueryValue(targetUrl, "year_of_publication");

    /// <summary>
    /// Every recognized advanced-search field present on the URL, as ready-to-display tags,
    /// in a fixed display order. This is the single source of truth for "what does this PEDro
    /// search cover" - callers (API responses, digest emails) should use this rather than
    /// re-deriving their own subset of fields.
    /// </summary>
    public static List<string> GetKeywordTags(string targetUrl)
    {
        var tags = new List<string>();

        void AddIfPresent(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                tags.Add(value);
            }
        }

        AddIfPresent(GetBodyPartLabel(targetUrl));
        AddIfPresent(GetTherapyLabel(targetUrl));
        AddIfPresent(GetProblemLabel(targetUrl));
        AddIfPresent(GetTopicLabel(targetUrl));
        AddIfPresent(GetSubdisciplineLabel(targetUrl));
        AddIfPresent(GetMethodLabel(targetUrl));

        var publicationYear = GetPublicationYear(targetUrl);
        if (!string.IsNullOrWhiteSpace(publicationYear))
        {
            tags.Add($"Since {publicationYear}");
        }

        var title = GetRawQueryValue(targetUrl, "title");
        if (!string.IsNullOrWhiteSpace(title))
        {
            tags.Add($"Title: \"{title}\"");
        }

        var abstractWithTitle = GetRawQueryValue(targetUrl, "abstract_with_title");
        if (!string.IsNullOrWhiteSpace(abstractWithTitle))
        {
            tags.Add($"Search: \"{abstractWithTitle}\"");
        }

        // PEDro's simple search (search.pedro.org.au/search-results?calc_text=...) is a
        // separate, simpler form from the advanced search fields above - only ever present
        // on its own, never alongside them.
        var calcText = GetRawQueryValue(targetUrl, "calc_text");
        if (!string.IsNullOrWhiteSpace(calcText))
        {
            tags.Add($"Search: \"{calcText}\"");
        }

        var source = GetRawQueryValue(targetUrl, "source");
        if (!string.IsNullOrWhiteSpace(source))
        {
            tags.Add($"Source: \"{source}\"");
        }

        var authorsAssociation = GetRawQueryValue(targetUrl, "authors_association");
        if (!string.IsNullOrWhiteSpace(authorsAssociation))
        {
            tags.Add($"Author: \"{authorsAssociation}\"");
        }

        var score = GetRawQueryValue(targetUrl, "nscore");
        if (!string.IsNullOrWhiteSpace(score))
        {
            tags.Add($"Score ≥ {score}");
        }

        var dateRecordWasCreated = GetRawQueryValue(targetUrl, "date_record_was_created");
        if (!string.IsNullOrWhiteSpace(dateRecordWasCreated))
        {
            tags.Add($"Added since {dateRecordWasCreated}");
        }

        return tags;
    }

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
