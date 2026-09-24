using RecordService.Models;

namespace RecordService.BusinessLogic.ChatService;

public interface IChatService
{
    Task<ChatResult> AskAsync(int userId, string question, IReadOnlyList<ChatTurn> history);
}
