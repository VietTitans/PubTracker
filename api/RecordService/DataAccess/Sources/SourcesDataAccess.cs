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

    public async Task<Paged<Source>> GetSourcesPageAsync(int page, int pageSize)
    {
        var totalCount = await _dbContext.Sources.CountAsync();
        var items = await _dbContext.Sources
            .OrderBy(s => s.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new Source { Id = s.Id, Name = s.Name, BaseUrl = s.BaseUrl })
            .ToListAsync();
        return new Paged<Source> { Items = items, TotalCount = totalCount };
    }

    public async Task<Source?> GetSourceByIdAsync(int sourceId)
    {
        return await _dbContext.Sources
            .Where(s => s.Id == sourceId)
            .Select(s => new Source { Id = s.Id, Name = s.Name, BaseUrl = s.BaseUrl })
            .FirstOrDefaultAsync();
    }
}
