using RecordData;
using RecordService.DataAccess;

namespace RecordService.BusinessLogic;

public class SourcesService : ISourcesService
{
    private readonly ISourcesDataAccess _dataAccess;

    public SourcesService(ISourcesDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    public async Task<List<Source>> GetAllSourcesAsync()
    {
        return await _dataAccess.GetAllSourcesAsync();
    }
}
