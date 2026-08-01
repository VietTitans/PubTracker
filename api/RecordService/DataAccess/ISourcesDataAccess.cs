using RecordData;

namespace RecordService.DataAccess;

public interface ISourcesDataAccess
{
    Task<List<Source>> GetAllSourcesAsync();
}
