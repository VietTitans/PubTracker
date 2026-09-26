using RecordService.Models;

namespace RecordService.DataAccess.Summarization;

/// <summary>
/// Vendor-agnostic digest summarization surface. All provider-specific integration details
/// (auth scheme, request shape, base URL) live behind implementations of this interface,
/// so swapping LLM providers only requires a new implementation plus a Program.cs registration change.
/// </summary>
public interface ISummaryGenerator
{
    Task<string> SummarizeAsync(IReadOnlyList<LiteratureRecord> records);
}
