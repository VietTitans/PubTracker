using RecordData;
using RecordService.DTOs;
using RecordService.DTOs.Source;

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

    public static PagedResponseDto<SourceResponseDto> ToPagedResponseDto(this Paged<Source> sources, PageRequest pageRequest)
    {
        return sources.ToPagedResponseDto(pageRequest, s => s.ToResponseDto());
    }
}
