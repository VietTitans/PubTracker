using System.ComponentModel.DataAnnotations;

namespace RecordService.DTOs.SearchQueryDto;

public class CreateSearchQueryDto
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "A URL is required.")]
    [StringLength(SearchQueryFieldLimits.TargetUrl)]
    [AbsoluteUri]
    public string TargetUrl { get; set; } = string.Empty;
}

public static class SearchQueryFieldLimits
{
    public const int TargetUrl = 2048;
}

/// <summary>
/// Rejects values that do not parse as an absolute URI. Scheme/host allowlisting is left to
/// each literature provider's CanHandle.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AbsoluteUriAttribute : ValidationAttribute
{
    public AbsoluteUriAttribute() : base("The value must be an absolute URL.") { }

    public override bool IsValid(object? value) =>
        value is not string s || string.IsNullOrWhiteSpace(s) || Uri.TryCreate(s.Trim(), UriKind.Absolute, out _);
}
