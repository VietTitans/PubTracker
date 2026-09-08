using RecordData;
using RecordService.BusinessLogic.DigestService;
using RecordService.DataAccess.ExternalSources;
using RecordService.DTOs.SearchQueryDto;
using RecordService.Models;

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
            Tags = GetKeywordTags(searchQuery.TargetUrl)
        };
    }

    private static List<string> GetKeywordTags(string targetUrl)
    {
        return SourceDetector.DetectSource(targetUrl) switch
        {
            SourceDetector.SourceType.PubMed => PubMedDigestMessageBuilder.GetKeywordTags(targetUrl),
            SourceDetector.SourceType.Pedro => PedroDigestMessageBuilder.GetKeywordTags(targetUrl),
            _ => new List<string>()
        };
    }

    public static List<SearchQueryResponseDto> ToResponseDtoList(this List<SearchQuery> searchQueries)
    {
        return searchQueries.Select(s => s.ToResponseDto()).ToList();
    }

    public static PollResultResponseDto ToResponseDto(this PollResult pollResult)
    {
        return new PollResultResponseDto
        {
            SearchQueryId = pollResult.SearchQueryId,
            IsSuccessful = pollResult.IsSuccessful,
            NewRecordCount = pollResult.NewRecordCount,
            ErrorMessage = pollResult.ErrorMessage
        };
    }
}
