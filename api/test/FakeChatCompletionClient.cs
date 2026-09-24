using RecordService.DataAccess.Chat;
using RecordService.Models;

namespace test;

/// <summary>
/// Returns a canned answer without calling Anthropic, so tests can exercise the chat endpoint's
/// retrieval/scoping behavior without live network access or a real API key.
/// </summary>
public class FakeChatCompletionClient : IChatCompletionClient
{
    public Task<string> GenerateAnswerAsync(string question, IReadOnlyList<ChatTurn> history, IReadOnlyList<LiteratureRecord> contextRecords, CancellationToken ct = default)
    {
        return Task.FromResult("Fake answer.");
    }
}
