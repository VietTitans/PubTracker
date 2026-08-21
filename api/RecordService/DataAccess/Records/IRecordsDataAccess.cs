using RecordService.Models;

namespace RecordService.DataAccess;

public interface IRecordsDataAccess
{
    /// <summary>
    /// Upserts the given records, links them to the source and search query, and reports
    /// how many were newly seen for this search query.
    /// </summary>
    /// <returns>The number of records newly linked to <paramref name="searchQueryId"/>.</returns>
    Task<int> PersistSearchResultsAsync(int searchQueryId, int sourceId, IReadOnlyList<LiteratureRecord> records);
}
