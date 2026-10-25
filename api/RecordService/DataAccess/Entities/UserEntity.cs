namespace RecordService.DataAccess.Entities;

public class UserEntity
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? KeycloakSub { get; set; }
    public bool DigestsStopped { get; set; }
    public bool IsMarkedForDeletion { get; set; }
    public DateTime? DeletionRequestedAt { get; set; }
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
    public DateTime? InactivityNoticeSentAt { get; set; }
}
