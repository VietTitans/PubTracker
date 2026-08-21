using RecordData;

namespace RecordService.BusinessLogic.SourcesService.SourcesService;

public interface ISourcesService
{
    Task<Source?> GetSourceByIdAsync(int sourceId);
    Task<List<Source>> GetAllSourcesAsync();
}
