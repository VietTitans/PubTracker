namespace RecordService.DTOs.SearchQueryDto;

public class SearchQueryResponseDto
{
    public int Id { get; set; }

    public int SourceId { get; set; }

    public string TargetUrl { get; set; } = string.Empty;

    public List<string>? Subscribers { get; set; }
}
