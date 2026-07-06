namespace RecordData;

class SearchQuery
{
    public int Id { get; set; }
    public int SourceId { get; set; }
    public string TargetUrl { get; set; } = string.Empty;
    public int CurrentRecordCount { get; set; }
    public int PreviousRecordCount { get; set; }

    public bool hasNewRecords; 

}
