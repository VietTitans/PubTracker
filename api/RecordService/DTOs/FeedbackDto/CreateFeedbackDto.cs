using System.ComponentModel.DataAnnotations;

namespace RecordService.DTOs.FeedbackDto;

public class CreateFeedbackDto
{
    [Required]
    [RegularExpression("^(bug|idea|praise)$", ErrorMessage = "Type must be bug, idea or praise.")]
    public string Type { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "A message is required.")]
    [StringLength(FeedbackFieldLimits.Message)]
    public string Message { get; set; } = string.Empty;
}

public static class FeedbackFieldLimits
{
    public const int Type = 10;
    public const int Message = 2000;
}
