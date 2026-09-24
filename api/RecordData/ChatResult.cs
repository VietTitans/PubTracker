namespace RecordService.Models;

/// <summary>
/// Outcome of a RAG chat query: a generated answer plus the tracked records it was grounded in.
/// </summary>
public class ChatResult
{
    public required string Answer { get; set; }
    public List<LiteratureRecord> CitedRecords { get; set; } = new();
}
