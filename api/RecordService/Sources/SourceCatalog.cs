using RecordService.BusinessLogic.DigestService;
using RecordService.DataAccess.ExternalSources;
using RecordService.Models;

namespace RecordService.Sources;

/// <summary>
/// Everything the system knows about one literature source. Name is the config key
/// (Sources:&lt;Name&gt;:Enabled) and the substring UrlMarker sniffs in a URL. EnabledByDefault is
/// the only place a default lives and must be false for a source that needs its owner's
/// permission (scraping). Digest and tag formatting stay available when a source is disabled,
/// so queries stored before it was disabled still render.
/// </summary>
public record SourceDefinition(
    string Name,
    string UrlMarker,
    bool EnabledByDefault,
    Type Provider,
    Action<IServiceCollection, IConfiguration> Register,
    Func<string, IReadOnlyList<LiteratureRecord>, string?, string> BuildDigest,
    Func<string, List<string>> GetKeywordTags);

public static class SourceCatalog
{
    // One row per source: add a row to add a source.
    public static readonly IReadOnlyList<SourceDefinition> All =
    [
        new("PubMed", "pubmed", true, typeof(PubMedProvider),
            PubMedHttpClientExtensions.AddPubMedSource,
            PubMedDigestMessageBuilder.BuildHtmlBody, PubMedDigestMessageBuilder.GetKeywordTags),
        new("Pedro", "pedro", false, typeof(PedroProvider),
            (services, _) => services.AddSingleton<PedroProvider>(),
            PedroDigestMessageBuilder.BuildHtmlBody, PedroDigestMessageBuilder.GetKeywordTags),
    ];

    public static SourceDefinition? Detect(string url) =>
        string.IsNullOrWhiteSpace(url)
            ? null
            : All.FirstOrDefault(s => url.Contains(s.UrlMarker, StringComparison.OrdinalIgnoreCase));

    public static IServiceCollection AddLiteratureSources(this IServiceCollection services, IConfiguration config)
    {
        var enabled = All.Where(s => bool.TryParse(config[$"Sources:{s.Name}:Enabled"], out var flag) ? flag : s.EnabledByDefault).ToList();
        foreach (var source in enabled)
            source.Register(services, config);
        return services.AddSingleton(sp => new LiteratureSourceFactory(
            enabled.Select(s => (ILiteratureSourceProvider)sp.GetRequiredService(s.Provider)).ToArray()));
    }
}
