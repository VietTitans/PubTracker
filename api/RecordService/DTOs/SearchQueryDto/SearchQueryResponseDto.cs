namespace RecordService.DTOs.SearchQueryDto;

public class SearchQueryResponseDto
{
    public int Id { get; set; }

    public int SourceId { get; set; }

    public string TargetUrl { get; set; } = string.Empty;

    public List<string>? Subscribers { get; set; }

    public DateTime? LastDigestSentAt { get; set; }

    public int RecordCount { get; set; }

    /// <summary>
    /// Every recognized PEDro advanced-search field present on TargetUrl, as ready-to-display
    /// tags (see PedroDigestMessageBuilder.GetKeywordTags). Empty for non-PEDro sources or a
    /// PEDro URL with no recognized fields.
    /// </summary>
    public List<string> Tags { get; set; } = new();
}
