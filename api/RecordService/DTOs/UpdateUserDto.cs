namespace RecordService.Dtos;

/// <summary>
/// DTO for updating an existing user
/// </summary>
public class UpdateUserDto
{
    public string Name { get; set; }

    public string Username { get; set; }

    public string Email { get; set; }
}
