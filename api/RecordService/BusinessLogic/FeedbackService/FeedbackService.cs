using RecordService.DataAccess;

namespace RecordService.BusinessLogic.FeedbackService;

public class FeedbackService : IFeedbackService
{
    private readonly IFeedbackDataAccess _dataAccess;

    public FeedbackService(IFeedbackDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    public async Task SubmitFeedbackAsync(int userId, string type, string message)
    {
        await _dataAccess.CreateFeedbackAsync(userId, type, message.Trim());
    }
}
