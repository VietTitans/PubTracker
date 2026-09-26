namespace RecordService.DataAccess.Entities;

public class SearchQueryRecordEntity
{
    public int SearchQueryId { get; set; }
    public int RecordId { get; set; }
    public DateTime? FirstSeenAt { get; set; }
}
