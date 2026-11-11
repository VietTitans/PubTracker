using RecordService.Authentication;
using RecordService.DataAccess;

namespace RecordService.BusinessLogic.FeedbackService;

public class FeedbackService : IFeedbackService
{
    private readonly IFeedbackDataAccess _dataAccess;

    public FeedbackService(IFeedbackDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    public async Task SubmitFeedbackAsync(Caller caller, int userId, string type, string message)
    {
        caller.RequireSelf(userId);
        await _dataAccess.CreateFeedbackAsync(userId, type, message.Trim());
    }
}
