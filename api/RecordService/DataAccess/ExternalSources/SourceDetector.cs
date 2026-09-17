namespace RecordService.DataAccess.ExternalSources;

/// <summary>
/// Utility class to detect literature source type from a URL.
/// Determines which provider should handle a given URL.
/// </summary>
public static class SourceDetector
{
    /// <summary>
    /// Enum representing supported literature sources
    /// </summary>
    public enum SourceType
    {
        Unknown = 0,
        PubMed = 1,
        Pedro = 2
    }

    /// <summary>
    /// Detects the source type based on URL content
    /// </summary>
    /// <param name="url">The URL to analyze</param>
    /// <returns>SourceType enum value</returns>
    public static SourceType DetectSource(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return SourceType.Unknown;

        var lowerUrl = url.ToLowerInvariant();

        // Check for PubMed indicators
        if (lowerUrl.Contains("pubmed"))
            return SourceType.PubMed;

        // Check for PEDro indicators
        if (lowerUrl.Contains("pedro"))
            return SourceType.Pedro;

        return SourceType.Unknown;
    }

    /// <summary>
    /// Gets the provider name from a source type
    /// </summary>
    public static string GetProviderName(SourceType sourceType)
    {
        return sourceType switch
        {
            SourceType.PubMed => "PubMed",
            SourceType.Pedro => "PEDro",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Checks if a source type is supported
    /// </summary>
    public static bool IsSupported(SourceType sourceType)
    {
        return sourceType is not SourceType.Unknown;
    }
}
