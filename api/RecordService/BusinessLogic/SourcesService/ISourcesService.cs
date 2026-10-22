using RecordData;

namespace RecordService.BusinessLogic.SourcesService;

public interface ISourcesService
{
    Task<Source?> GetSourceByIdAsync(int sourceId);
    Task<Paged<Source>> GetSourcesPageAsync(int page, int pageSize);
}
