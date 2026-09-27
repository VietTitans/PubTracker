using RecordService.Models;

namespace RecordService.DataAccess;

public class PersistResult
{
    /// <summary>The subset of the input records newly linked to the search query being persisted.</summary>
    public required List<LiteratureRecord> NewlyLinkedRecords { get; init; }

    /// <summary>The subset of the input records that were genuinely new rows in the records table (not just newly linked to this query).</summary>
    public required List<LiteratureRecord> NewlyInsertedRecords { get; init; }
}
