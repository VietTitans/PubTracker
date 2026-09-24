namespace RecordService.DTOs.ChatDto;

public class ChatResponseDto
{
    public string Answer { get; set; } = string.Empty;
    public List<CitedRecordDto> Citations { get; set; } = new();
}
