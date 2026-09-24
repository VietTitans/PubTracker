using RecordService.Models;

namespace RecordService.DataAccess.Chat;

public interface IChatCompletionClient
{
    /// <summary>
    /// Generates an answer to <paramref name="question"/> grounded in <paramref name="contextRecords"/>,
    /// with <paramref name="history"/> giving the model the prior turns of this conversation.
    /// </summary>
    Task<string> GenerateAnswerAsync(string question, IReadOnlyList<ChatTurn> history, IReadOnlyList<LiteratureRecord> contextRecords, CancellationToken ct = default);
}
