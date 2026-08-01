using RecordData;
using RecordService.Dtos;

namespace RecordService.Extensions;

public static class SourceMappingExtensions
{
    public static SourceResponseDto ToResponseDto(this Source source)
    {
        return new SourceResponseDto
        {
            Id = source.Id,
            Name = source.Name,
            BaseUrl = source.BaseUrl
        };
    }

    public static List<SourceResponseDto> ToResponseDtoList(this List<Source> sources)
    {
        return sources.Select(s => s.ToResponseDto()).ToList();
    }
}
