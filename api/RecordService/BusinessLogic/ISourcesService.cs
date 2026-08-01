using RecordData;

namespace RecordService.BusinessLogic;

public interface ISourcesService
{
    Task<List<Source>> GetAllSourcesAsync();
}
