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
    /// PEDro-specific decoded search-field labels (see PedroDigestMessageBuilder). Null for
    /// non-PEDro sources, or when the URL doesn't carry a recognized value for that field.
    /// </summary>
    public string? Topic { get; set; }

    public string? Therapy { get; set; }

    public string? Problem { get; set; }

    public string? BodyPart { get; set; }

    public string? PublicationYear { get; set; }
}
