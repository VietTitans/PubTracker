using System.Text.RegularExpressions;
using RecordService.Models;

namespace RecordService.BusinessLogic.DigestService;

/// <summary>
/// Best-effort topic classification for a digest's new records, so DigestMessageFormatter can
/// group records instead of listing hundreds of titles flat. Interim and keyword-rule based:
/// against a real 849-record PubMed digest this only named a cluster for about a third of
/// records (the rest fall to DigestMessageFormatter's "Other" bucket) - a real implementation
/// likely wants an LLM pass over title + abstract instead of more regexes, and per-query
/// cluster names instead of a fixed list, once sources outside healthcare are supported.
/// Order matters: the first matching cluster wins, so more specific rules are listed first.
/// </summary>
internal static class RecordTopicClassifier
{
    private static readonly (string Cluster, Regex Pattern)[] Clusters =
    {
        ("Systematic reviews & meta-analyses",
            new Regex(@"systematic review|meta-analysis", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("RCT protocols",
            new Regex(@"study protocol|protocol for a|randomi[sz]ed controlled trial protocol", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Exercise & movement-based therapy",
            new Regex(@"\bexercise\b|\byoga\b|pilates|stretching|physical activity|physical therapy", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Telerehabilitation & digital delivery",
            new Regex(@"telerehabilitation|telehealth|digital health|mobile app|app-based", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Pediatric, adolescent & youth-specific",
            new Regex(@"adolescen|p[a]?ediatric|\bchild\b|\byouth\b|young adult", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Surgical & interventional",
            new Regex(@"surger|surgical|injection|discectomy|fusion|ablation", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Imaging & diagnosis",
            new Regex(@"imaging|\bMRI\b|radiograph|diagnos|kinematic", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("Psychological & biopsychosocial",
            new Regex(@"psycholog|cognitive|biopsychosocial|fear-avoidance|catastrophi", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
    };

    /// <summary>Fixed display order for clusters - the same order rules are tried in.</summary>
    public static IReadOnlyList<string> DisplayOrder { get; } = Clusters.Select(c => c.Cluster).ToArray();

    /// <summary>Cluster name for the record's title, or null if no rule matched (goes in "Other").</summary>
    public static string? Classify(LiteratureRecord record)
    {
        foreach (var (cluster, pattern) in Clusters)
        {
            if (pattern.IsMatch(record.Title))
            {
                return cluster;
            }
        }

        return null;
    }
}
