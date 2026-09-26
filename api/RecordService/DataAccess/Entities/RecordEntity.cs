namespace RecordService.DataAccess.Entities;

public class RecordEntity
{
    public int Id { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string? Doi { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SourceUrl { get; set; }
}
