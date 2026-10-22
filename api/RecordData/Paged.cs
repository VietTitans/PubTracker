namespace RecordData;

/// <summary>One page of a larger result set, plus the size of the whole set.</summary>
public class Paged<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
}
