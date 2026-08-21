namespace RecordService.Models;

/// <summary>
/// Outcome of polling a single search query for new records during a scheduled poll cycle.
/// </summary>
public class PollResult
{
    public int SearchQueryId { get; set; }
    public bool IsSuccessful { get; set; }
    public int NewRecordCount { get; set; }
    public string? ErrorMessage { get; set; }
}
