using Microsoft.EntityFrameworkCore;
using RecordData;

namespace RecordService.DataAccess;

public class SourcesDataAccess : ISourcesDataAccess
{
    private readonly PubTrackerDbContext _dbContext;

    public SourcesDataAccess(PubTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<Source>> GetAllSourcesAsync()
    {
        return await _dbContext.Sources
            .Select(s => new Source { Id = s.Id, Name = s.Name, BaseUrl = s.BaseUrl })
            .ToListAsync();
    }

    public async Task<Source?> GetSourceByIdAsync(int sourceId)
    {
        return await _dbContext.Sources
            .Where(s => s.Id == sourceId)
            .Select(s => new Source { Id = s.Id, Name = s.Name, BaseUrl = s.BaseUrl })
            .FirstOrDefaultAsync();
    }
}
