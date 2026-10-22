using System.ComponentModel.DataAnnotations;

namespace RecordService.DTOs.DatabaseSuggestionDto;

public class CreateDatabaseSuggestionDto
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "A name is required.")]
    [StringLength(DatabaseSuggestionFieldLimits.Name)]
    public string Name { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "A website is required.")]
    [StringLength(DatabaseSuggestionFieldLimits.WebsiteUrl)]
    [RegularExpression(@"^https?://\S+$", ErrorMessage = "The website must be an http(s) URL.")]
    public string WebsiteUrl { get; set; } = string.Empty;

    [StringLength(DatabaseSuggestionFieldLimits.Reason)]
    public string? Reason { get; set; }
}

public static class DatabaseSuggestionFieldLimits
{
    public const int Name = 200;
    public const int WebsiteUrl = 2048;
    public const int Reason = 2000;
}
