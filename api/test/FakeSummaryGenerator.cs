using RecordService.DataAccess.Summarization;
using RecordService.Models;

namespace test;

public class FakeSummaryGenerator : ISummaryGenerator
{
    public string FixedSummary { get; set; } = string.Empty;

    /// <summary>When true, SummarizeAsync throws, so tests can prove a summary failure never blocks digest sending.</summary>
    public bool ShouldThrow { get; set; }

    public List<IReadOnlyList<LiteratureRecord>> Calls { get; } = new();

    public Task<string> SummarizeAsync(IReadOnlyList<LiteratureRecord> records)
    {
        if (ShouldThrow)
        {
            throw new InvalidOperationException("Simulated summary generation failure");
        }

        Calls.Add(records);
        return Task.FromResult(FixedSummary);
    }
}
