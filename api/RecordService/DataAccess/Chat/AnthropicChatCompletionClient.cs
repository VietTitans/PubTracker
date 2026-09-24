using System.Text;
using Anthropic;
using Anthropic.Models.Messages;
using RecordService.Models;

namespace RecordService.DataAccess.Chat;

/// <summary>
/// Generates chat answers grounded in a small set of retrieved literature records.
/// </summary>
public class AnthropicChatCompletionClient : IChatCompletionClient
{
    private const string Model = "claude-sonnet-5";

    // Adaptive thinking is on by default for this model and counts against MaxTokens, so a
    // low cap sized only for the visible answer risks the response being truncated before any
    // TextBlock is written - stay near the SDK's non-streaming default instead.
    private const int MaxTokens = 16000;

    private readonly AnthropicClient _client;

    public AnthropicChatCompletionClient(string apiKey)
    {
        _client = new AnthropicClient { ApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey)) };
    }

    // contextRecords are retrieved fresh for `question` only - prior turns give the model
    // conversational context (so "what about X" resolves correctly), not their own citations.
    public async Task<string> GenerateAnswerAsync(string question, IReadOnlyList<ChatTurn> history, IReadOnlyList<LiteratureRecord> contextRecords, CancellationToken ct = default)
    {
        var messages = history
            .Select(turn => new MessageParam { Role = turn.Role == "assistant" ? Role.Assistant : Role.User, Content = turn.Text })
            .Append(new MessageParam { Role = Role.User, Content = question })
            .ToList();

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model,
            MaxTokens = MaxTokens,
            OutputConfig = new OutputConfig { Effort = Effort.Low },
            System = BuildSystemPrompt(contextRecords),
            Messages = messages
        }, ct);

        var answer = string.Join("\n", response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));

        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new InvalidOperationException($"Anthropic returned no text (stop reason: {response.StopReason}).");
        }

        return answer;
    }

    private static string BuildSystemPrompt(IReadOnlyList<LiteratureRecord> contextRecords)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You answer questions about literature records the user tracks in PubTracker.");
        sb.AppendLine("Answer only using the records listed below. If they don't contain enough information to answer, say so instead of guessing.");
        // Each turn re-retrieves records independently, so a positional index like [1] can point
        // at a different paper next turn. ExternalId is the record's permanent dedup key, so
        // citing by it stays correct even when an earlier turn is being referenced.
        sb.AppendLine("Cite a record by its ID exactly as shown in brackets below (e.g. [pubmed:12345]) - never by position number, since a record's position can differ between turns.");
        sb.AppendLine();

        foreach (var record in contextRecords)
        {
            sb.AppendLine($"[{record.ExternalId}] Title: {record.Title}");
            if (!string.IsNullOrWhiteSpace(record.Abstract))
                sb.AppendLine($"Abstract: {record.Abstract}");
            if (!string.IsNullOrWhiteSpace(record.SourceUrl))
                sb.AppendLine($"Source: {record.SourceUrl}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
