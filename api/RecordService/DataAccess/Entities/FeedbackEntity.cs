namespace RecordService.DataAccess.Entities;

public class FeedbackEntity
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
