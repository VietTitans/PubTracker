using RecordData;
using RecordService.Sources;
using RecordService.DTOs;
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
            Subscribers = searchQuery.Subscribers,
            LastDigestSentAt = searchQuery.LastDigestSentAt,
            RecordCount = searchQuery.RecordCount,
            LastFetchedAt = searchQuery.LastFetchedAt,
            SourceRecordCount = searchQuery.SourceRecordCount,
            Tags = GetKeywordTags(searchQuery.TargetUrl),
            LastPollFailedAt = searchQuery.LastPollFailedAt
        };
    }

    private static List<string> GetKeywordTags(string targetUrl)
    {
        return SourceCatalog.Detect(targetUrl)?.GetKeywordTags(targetUrl) ?? new List<string>();
    }

    public static PagedResponseDto<SearchQueryResponseDto> ToPagedResponseDto(this Paged<SearchQuery> searchQueries, PageRequest pageRequest)
    {
        return searchQueries.ToPagedResponseDto(pageRequest, s => s.ToResponseDto());
    }

    public static List<SearchQueryResponseDto> ToResponseDtoList(this List<SearchQuery> searchQueries)
    {
        return searchQueries.Select(s => s.ToResponseDto()).ToList();
    }
}
