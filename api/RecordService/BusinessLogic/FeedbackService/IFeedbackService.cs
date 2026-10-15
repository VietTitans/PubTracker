namespace RecordService.BusinessLogic.FeedbackService;

public interface IFeedbackService
{
    Task SubmitFeedbackAsync(int userId, string type, string message);
}
