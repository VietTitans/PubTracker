using RecordData;

namespace RecordService.DataAccess;

public interface ISourcesDataAccess
{
    Task<Paged<Source>> GetSourcesPageAsync(int page, int pageSize);
    Task<Source?> GetSourceByIdAsync(int sourceId);
}
