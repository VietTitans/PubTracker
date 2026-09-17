namespace RecordData;

public class User
{
    public int Id { get; set; }

    public string Name { get; set; }

    public string Username { get; set; }

    public string Email { get; set; }

    public string? KeycloakSub { get; set; }

    public bool IsMarkedForDeletion { get; set; } = false;

    public DateTime? DeletionRequestedAt { get; set; } = null;

}
