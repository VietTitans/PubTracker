using RecordService.Models;

namespace RecordService.DataAccess.ExternalSources;

/// <summary>
/// Strategy interface for literature source providers.
/// Supports both API-based sources (e.g., PubMed NCBI API) and HTML-based sources (e.g., PEDro).
/// Each implementation handles a specific literature database and manages its own data retrieval strategy.
/// </summary>
public interface ILiteratureSourceProvider
{
    /// <summary>
    /// Gets the name/type of this provider (e.g., "PubMed", "PEDro")
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Determines if this provider can handle the given URL.
    /// </summary>
    /// <param name="url">The source URL to check</param>
    /// <returns>True if this provider can handle the URL, false otherwise</returns>
    bool CanHandle(string url);

    /// <summary>
    /// Executes a search and returns new records found since the last run.
    /// 
    /// Implementation details are provider-specific:
    /// - API-based providers may call external REST APIs to fetch data
    /// - HTML-based providers may parse cached HTML content
    /// 
    /// The method transparently handles the comparison with previous results
    /// and returns only new records discovered since lastRunDate.
    /// </summary>
    /// <param name="url">The search URL from the source</param>
    /// <param name="lastRunDate">The date of the previous search run (for comparison). If null, all records are considered new.</param>
    /// <returns>SourceSearchResult containing new records count and list</returns>
    Task<SourceSearchResult> SearchAsync(string url, DateTime? lastRunDate = null);

    /// <summary>
    /// Refreshes data for the given URL.
    /// 
    /// Implementation depends on provider type:
    /// - API-based providers may validate API credentials or fetch fresh metadata
    /// - HTML-based providers may re-fetch and cache HTML content
    /// 
    /// Should be called periodically to keep data up-to-date.
    /// </summary>
    /// <param name="url">The source URL to refresh</param>
    /// <returns>True if refresh was successful, false otherwise</returns>
    Task<bool> RefreshAsync(string url);
}
