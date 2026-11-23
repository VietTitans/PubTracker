using RecordService.Authentication;

namespace RecordService.BusinessLogic.FeedbackService;

public interface IFeedbackService
{
    Task SubmitFeedbackAsync(Caller caller, int userId, string type, string message);
}
