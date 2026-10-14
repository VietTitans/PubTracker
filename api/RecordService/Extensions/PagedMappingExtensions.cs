using RecordData;
using RecordService.DTOs;

namespace RecordService.Extensions;

public static class PagedMappingExtensions
{
    public static PagedResponseDto<TDto> ToPagedResponseDto<TModel, TDto>(
        this Paged<TModel> paged, PageRequest pageRequest, Func<TModel, TDto> toDto)
    {
        return new PagedResponseDto<TDto>
        {
            Items = paged.Items.Select(toDto).ToList(),
            Page = pageRequest.Page,
            PageSize = pageRequest.PageSize,
            TotalCount = paged.TotalCount
        };
    }
}
