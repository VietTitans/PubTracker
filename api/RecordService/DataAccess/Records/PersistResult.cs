using RecordService.Models;

namespace RecordService.DataAccess;

public class PersistResult
{
    /// <summary>The subset of the input records newly linked to the search query being persisted.</summary>
    public required List<LiteratureRecord> NewlyLinkedRecords { get; init; }
}
