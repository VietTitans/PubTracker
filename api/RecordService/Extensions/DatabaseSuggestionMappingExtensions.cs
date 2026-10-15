using RecordData;
using RecordService.DTOs;
using RecordService.DTOs.DatabaseSuggestionDto;

namespace RecordService.Extensions;

public static class DatabaseSuggestionMappingExtensions
{
    public static DatabaseSuggestionResponseDto ToResponseDto(this DatabaseSuggestion suggestion)
    {
        return new DatabaseSuggestionResponseDto
        {
            Id = suggestion.Id,
            Name = suggestion.Name,
            WebsiteUrl = suggestion.WebsiteUrl,
            VoteCount = suggestion.VoteCount,
            HasVoted = suggestion.HasVoted
        };
    }

    public static PagedResponseDto<DatabaseSuggestionResponseDto> ToPagedResponseDto(this Paged<DatabaseSuggestion> suggestions, PageRequest pageRequest)
    {
        return suggestions.ToPagedResponseDto(pageRequest, s => s.ToResponseDto());
    }
}
