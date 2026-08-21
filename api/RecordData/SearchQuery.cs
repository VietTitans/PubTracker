namespace RecordData;
public class SearchQuery
{
    public int Id { get; set; }

    public int SourceId { get; set; }
    public string TargetUrl { get; set; } = string.Empty;
    public List<string>? Subscribers { get; set; }
    public DateTime? LastDigestSentAt { get; set; }

}
