namespace RecordService.DTOs.UserDto;

/// <summary>
/// DTO for creating a new user
/// </summary>
public class CreateUserDto
{
    public string Name { get; set; }

    public string Username { get; set; }

    public string Email { get; set; }
}
