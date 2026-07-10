namespace RecordData;

class SearchQuery
{
    public int SourceId { get; set; }
    public string TargetUrl { get; set; } = string.Empty;
    public List<string>? Subscriptioners { get; set; }
    public int CurrentRecordCount { get; set; }
    public int PreviousRecordCount { get; set; }

    public bool hasNewRecords; 

}
