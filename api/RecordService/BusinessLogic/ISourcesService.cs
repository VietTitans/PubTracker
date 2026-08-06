using RecordData;

namespace RecordService.BusinessLogic;

public interface ISourcesService
{
    Task<Source?> GetSourceByIdAsync(int sourceId);
    Task<List<Source>> GetAllSourcesAsync();
}
