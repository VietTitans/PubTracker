namespace RecordService.DTOs.ChatDto;

public class CitedRecordDto
{
    public string ExternalId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Doi { get; set; }
    public string? SourceUrl { get; set; }
}
