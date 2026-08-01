using RecordData;
using RecordService.Dtos;

namespace RecordService.Extensions;

public static class SavedSearchMappingExtensions
{
    public static SavedSearchResponseDto ToResponseDto(this SavedSearch savedSearch)
    {
        return new SavedSearchResponseDto
        {
            Id = savedSearch.Id,
            UserId = savedSearch.UserId,
            SearchQueryId = savedSearch.SearchQueryId,
            CreatedAt = savedSearch.CreatedAt,
            SearchQuery = savedSearch.SearchQuery?.ToResponseDto()
        };
    }

    public static List<SavedSearchResponseDto> ToResponseDtoList(this List<SavedSearch> savedSearches)
    {
        return savedSearches.Select(s => s.ToResponseDto()).ToList();
    }
}
