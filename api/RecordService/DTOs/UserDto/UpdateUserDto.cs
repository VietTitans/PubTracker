using System.ComponentModel.DataAnnotations;

namespace RecordService.DTOs.UserDto;

/// <summary>
/// DTO for updating an existing user
/// </summary>
public class UpdateUserDto
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(UserFieldLimits.Name)]
    public string Name { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(UserFieldLimits.Username)]
    public string Username { get; set; }

    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    [StringLength(UserFieldLimits.Email)]
    public string Email { get; set; }
}
