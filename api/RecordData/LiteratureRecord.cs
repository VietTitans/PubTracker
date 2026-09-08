namespace RecordService.Models;

/// <summary>
/// Represents a single literature record/publication from an external source.
/// Contains essential metadata for tracking and comparison across search runs.
/// </summary>
public class LiteratureRecord
{
    /// <summary>
    /// Globally unique identifier for this record within its source (e.g. "pubmed:12345",
    /// "pedro:6789"). Used as the dedup key, since a real DOI isn't always present and can
    /// be shared by distinct records (e.g. a review and its later update).
    /// </summary>
    public required string ExternalId { get; set; }

    /// <summary>
    /// The record's real DOI, when one exists. Display-only - not used for deduplication.
    /// </summary>
    public string? Doi { get; set; }

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
