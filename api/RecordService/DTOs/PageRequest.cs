using System.ComponentModel.DataAnnotations;

namespace RecordService.DTOs;

/// <summary>Query-string paging parameters shared by every paginated list endpoint.</summary>
public class PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private int _pageSize = DefaultPageSize;

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>Values above <see cref="MaxPageSize"/> are clamped, not rejected.</summary>
    [Range(1, int.MaxValue)]
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = Math.Min(value, MaxPageSize);
    }
}
