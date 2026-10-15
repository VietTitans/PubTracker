namespace RecordData;

public class DatabaseSuggestion
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public int VoteCount { get; set; }
    public bool HasVoted { get; set; }
}
