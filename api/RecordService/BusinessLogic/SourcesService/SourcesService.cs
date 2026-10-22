using RecordData;
using RecordService.DataAccess;

namespace RecordService.BusinessLogic.SourcesService;

public class SourcesService : ISourcesService
{
    private readonly ISourcesDataAccess _dataAccess;

    public SourcesService(ISourcesDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    public async Task<Paged<Source>> GetSourcesPageAsync(int page, int pageSize)
    {
        return await _dataAccess.GetSourcesPageAsync(page, pageSize);
    }

    public async Task<Source?> GetSourceByIdAsync(int sourceId)
    {
        return await _dataAccess.GetSourceByIdAsync(sourceId);
    }
}
