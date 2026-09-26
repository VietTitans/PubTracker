namespace RecordService.DataAccess.Entities;

public class SearchQueryEntity
{
    public int Id { get; set; }
    public int? SourceId { get; set; }
    public string? TargetUrl { get; set; }
    public DateTime? LastDigestSentAt { get; set; }
    public DateTime? LastPolledAt { get; set; }
    public int? SourceRecordCount { get; set; }
}
