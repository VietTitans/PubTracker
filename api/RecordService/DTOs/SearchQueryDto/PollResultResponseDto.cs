namespace RecordService.DTOs.SearchQueryDto;

public class PollResultResponseDto
{
    public int SearchQueryId { get; set; }

    public bool IsSuccessful { get; set; }

    public int NewRecordCount { get; set; }

    public string? ErrorMessage { get; set; }
}
