using RecordData;

namespace RecordService.DataAccess;

public class SourcesDataAccess : ISourcesDataAccess
{
    // This is a placeholder. In a real implementation, this would interact with the database
    private static List<Source> _sources = new()
    {
        new Source { Id = 1, Name = "Source 1", BaseUrl = "https://example1.com" },
        new Source { Id = 2, Name = "Source 2", BaseUrl = "https://example2.com" }
    };

    public async Task<List<Source>> GetAllSourcesAsync()
    {
        return await Task.FromResult(_sources);
    }
}
