namespace RecordService.Dtos;

/// <summary>
/// DTO for user data returned in API responses
/// Does not include sensitive data
/// </summary>
public class UserResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; }

    public string Username { get; set; }

    public string Email { get; set; }
}
