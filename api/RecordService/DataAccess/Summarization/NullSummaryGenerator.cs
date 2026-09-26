using RecordService.Models;

namespace RecordService.DataAccess.Summarization;

/// <summary>
/// No-op summarizer used when no LLM provider is configured (Llm:ApiKey missing). Unlike email,
/// which is core and fails startup if unconfigured, the AI summary is additive - missing config
/// should just mean no summary, not a startup failure.
/// </summary>
public class NullSummaryGenerator : ISummaryGenerator
{
    public Task<string> SummarizeAsync(IReadOnlyList<LiteratureRecord> records) => Task.FromResult(string.Empty);
}
