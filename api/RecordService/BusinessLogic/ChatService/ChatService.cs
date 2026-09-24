using RecordService.DataAccess;
using RecordService.DataAccess.Chat;
using RecordService.DataAccess.Embeddings;
using RecordService.Models;

namespace RecordService.BusinessLogic.ChatService;

public class ChatService : IChatService
{
    private const int TopK = 8;

    private readonly IRecordsDataAccess _recordsDataAccess;
    private readonly IEmbeddingClient _embeddingClient;
    private readonly IChatCompletionClient _chatCompletionClient;

    public ChatService(IRecordsDataAccess recordsDataAccess, IEmbeddingClient embeddingClient, IChatCompletionClient chatCompletionClient)
    {
        _recordsDataAccess = recordsDataAccess;
        _embeddingClient = embeddingClient;
        _chatCompletionClient = chatCompletionClient;
    }

    public async Task<ChatResult> AskAsync(int userId, string question, IReadOnlyList<ChatTurn> history)
    {
        var questionEmbeddings = await _embeddingClient.EmbedBatchAsync([question]);
        var questionEmbedding = questionEmbeddings[0]
            ?? throw new InvalidOperationException("Failed to embed the chat question.");

        var contextRecords = await _recordsDataAccess.SearchSimilarRecordsAsync(userId, questionEmbedding, TopK);
        var answer = await _chatCompletionClient.GenerateAnswerAsync(question, history, contextRecords);

        return new ChatResult { Answer = answer, CitedRecords = contextRecords };
    }
}
