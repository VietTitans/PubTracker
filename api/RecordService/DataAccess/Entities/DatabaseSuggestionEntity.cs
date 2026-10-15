namespace RecordService.DataAccess.Entities;

public class DatabaseSuggestionEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
