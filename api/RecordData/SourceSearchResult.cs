namespace RecordService.Models;

/// <summary>
/// Result returned by a literature source provider after querying.
/// Contains new records found since the last search run.
/// </summary>
public class SourceSearchResult
{
    /// <summary>
    /// Total count of new records found since last run
    /// </summary>
    public int NewRecordCount { get; set; }

    /// <summary>
    /// List of new literature records discovered
    /// </summary>
    public List<LiteratureRecord> NewRecords { get; set; } = new();

    /// <summary>
    /// Timestamp of this search result
    /// </summary>
    public DateTime SearchExecutedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Source type that generated this result (e.g., "PubMed", "PEDro")
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Optional error message if search failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Indicates if the search was successful
    /// </summary>
    public bool IsSuccessful { get; set; } = true;
}
