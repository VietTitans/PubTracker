namespace RecordService.Dtos;

public class SavedSearchResponseDto
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int SearchQueryId { get; set; }

    public DateTime CreatedAt { get; set; }

    public SearchQueryResponseDto? SearchQuery { get; set; }
}
