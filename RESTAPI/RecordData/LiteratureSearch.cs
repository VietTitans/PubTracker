namespace RecordData;

class LiteratureSearch
{
    public Guid Id { get; set; }

    public User User { get; set; }

    public LiteratureDatabase LiteratureDatabase { get; set; }

    public string SearchQuery { get; set; }

    public DateTime PerformedAt { get; set; }

    public ICollection<SearchResult> Results { get; set; }


}
