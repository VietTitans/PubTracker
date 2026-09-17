namespace RecordService.DTOs.Source;

public class SourceResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; }

    public string BaseUrl { get; set; } = string.Empty;
}
