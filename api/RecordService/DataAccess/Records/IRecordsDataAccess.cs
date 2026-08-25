using RecordService.Models;

namespace RecordService.DataAccess;

public interface IRecordsDataAccess
{
    /// <summary>
    /// Upserts the given records and links them to the source and search query.
    /// </summary>
    /// <returns>The subset of <paramref name="records"/> newly linked to <paramref name="searchQueryId"/>.</returns>
    Task<List<LiteratureRecord>> PersistSearchResultsAsync(int searchQueryId, int sourceId, IReadOnlyList<LiteratureRecord> records);
}
