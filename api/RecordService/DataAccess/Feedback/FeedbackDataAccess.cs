using RecordService.DataAccess.Entities;

namespace RecordService.DataAccess;

public class FeedbackDataAccess : IFeedbackDataAccess
{
    private readonly PubTrackerDbContext _dbContext;

    public FeedbackDataAccess(PubTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task CreateFeedbackAsync(int userId, string type, string message)
    {
        _dbContext.Feedback.Add(new FeedbackEntity { UserId = userId, Type = type, Message = message });
        await _dbContext.SaveChangesAsync();
    }
}
