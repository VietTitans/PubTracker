namespace RecordService.DataAccess.Entities;

public class UserSearchQueryEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? SearchQueryId { get; set; }
    public DateTime CreatedAt { get; set; }
}
