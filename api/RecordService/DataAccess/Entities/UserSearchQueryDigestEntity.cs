namespace RecordService.DataAccess.Entities;

public class UserSearchQueryDigestEntity
{
    public int UserId { get; set; }
    public int SearchQueryId { get; set; }
    public DateTime? LastDigestSentAt { get; set; }
}
