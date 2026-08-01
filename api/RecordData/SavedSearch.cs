namespace RecordData;

public class SavedSearch
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public int SearchQueryId { get; set; }

    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }

    public SearchQuery? SearchQuery { get; set; }
}
