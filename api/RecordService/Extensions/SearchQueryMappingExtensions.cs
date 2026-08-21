using RecordData;
using RecordService.DTOs.SearchQueryDto;

namespace RecordService.Extensions;

public static class SearchQueryMappingExtensions
{
    public static SearchQueryResponseDto ToResponseDto(this SearchQuery searchQuery)
    {
        return new SearchQueryResponseDto
        {
            Id = searchQuery.Id,
            SourceId = searchQuery.SourceId,
            TargetUrl = searchQuery.TargetUrl,
            Subscribers = searchQuery.Subscribers
        };
    }

    public static List<SearchQueryResponseDto> ToResponseDtoList(this List<SearchQuery> searchQueries)
    {
        return searchQueries.Select(s => s.ToResponseDto()).ToList();
    }
}
