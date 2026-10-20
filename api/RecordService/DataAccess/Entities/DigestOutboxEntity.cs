namespace RecordService.DataAccess.Entities;

public class DigestOutboxEntity
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string ClaimsJson { get; set; } = "[]";
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
    /// <summary>Set with SentAt when no email went out (recipient gone or stopped Digests); not counted against the daily send limit.</summary>
    public bool IsSkipped { get; set; }
    public DateTime? FailedAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
}
