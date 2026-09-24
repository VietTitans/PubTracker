namespace RecordService.DTOs.ChatDto;

public class ChatRequestDto
{
    public string Question { get; set; } = string.Empty;
    public List<ChatMessageDto> History { get; set; } = new();
}
