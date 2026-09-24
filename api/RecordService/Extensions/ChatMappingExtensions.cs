using RecordService.DTOs.ChatDto;
using RecordService.Models;

namespace RecordService.Extensions;

public static class ChatMappingExtensions
{
    public static ChatResponseDto ToResponseDto(this ChatResult chatResult)
    {
        return new ChatResponseDto
        {
            Answer = chatResult.Answer,
            Citations = chatResult.CitedRecords.Select(ToCitedRecordDto).ToList()
        };
    }

    public static CitedRecordDto ToCitedRecordDto(this LiteratureRecord record)
    {
        return new CitedRecordDto
        {
            ExternalId = record.ExternalId,
            Title = record.Title,
            Doi = record.Doi,
            SourceUrl = record.SourceUrl
        };
    }

    public static ChatTurn ToChatTurn(this ChatMessageDto dto)
    {
        return new ChatTurn { Role = dto.Role, Text = dto.Text };
    }
}
