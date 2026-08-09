namespace RecordService.Models;

/// <summary>
/// Represents a single literature record/publication from an external source.
/// Contains essential metadata for tracking and comparison across search runs.
/// </summary>
public class LiteratureRecord
{
    /// <summary>
    /// Unique identifier from the record
    /// </summary>
    public required string Doi { get; set; }

    /// <summary>
    /// Title of the publication/record
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Authors of the publication
    /// </summary>
    public string Authors { get; set; } = string.Empty;

    /// <summary>
    /// Publication/Release date
    /// </summary>
    public DateTime? PublishedDate { get; set; }

    /// <summary>
    /// Abstract or summary of the publication
    /// </summary>
    public string? Abstract { get; set; }

    /// <summary>
    /// URL to the record on the external source
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Timestamp when this record was first discovered/cached
    /// </summary>
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Source type (e.g., "PubMed", "PEDro")
    /// </summary>
    public string Source { get; set; } = string.Empty;
}
