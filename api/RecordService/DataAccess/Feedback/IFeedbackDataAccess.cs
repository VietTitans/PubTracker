namespace RecordService.DataAccess;

public interface IFeedbackDataAccess
{
    Task CreateFeedbackAsync(int userId, string type, string message);
}
