using RecordService.DataAccess.Summarization;
using RecordService.Models;

namespace test;

public class FakeSummaryGenerator : ISummaryGenerator
{
    public string FixedSummary { get; set; } = string.Empty;

    /// <summary>When true, SummarizeAsync throws, so tests can prove a summary failure never blocks digest sending.</summary>
    public bool ShouldThrow { get; set; }

    public List<IReadOnlyList<LiteratureRecord>> Calls { get; } = new();

    public string AuthorIntentionFixed { get; set; } = string.Empty;

    /// <summary>When true, GenerateAuthorIntentionAsync throws, so tests can prove a failure never blocks polling.</summary>
    public bool AuthorIntentionShouldThrow { get; set; }

    public List<string> AuthorIntentionCalls { get; } = new();

    /// <summary>Simulated per-call latency (e.g. real LLM network round-trip time), applied to both methods below.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public async Task<string> SummarizeAsync(IReadOnlyList<LiteratureRecord> records)
    {
        await Task.Delay(Delay);

        if (ShouldThrow)
        {
            throw new InvalidOperationException("Simulated summary generation failure");
        }

        Calls.Add(records);
        return FixedSummary;
    }

    public async Task<string> GenerateAuthorIntentionAsync(LiteratureRecord record)
    {
        await Task.Delay(Delay);

        if (AuthorIntentionShouldThrow)
        {
            throw new InvalidOperationException("Simulated author intention generation failure");
        }

        AuthorIntentionCalls.Add(record.ExternalId);
        return AuthorIntentionFixed;
    }
}
