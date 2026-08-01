using RecordData;
using RecordService.Dtos;

namespace RecordService.Extensions;

/// Extension methods for mapping between User model and DTOs
public static class UserMappingExtensions
{
    /// Maps a User model to a UserResponseDto
    public static UserResponseDto ToResponseDto(this User user)
    {
        if (user == null)
            return null;

        return new UserResponseDto
        {
            Id = user.Id,
            Name = user.Name,
            Username = user.Username,
            Email = user.Email
        };
    }

    /// Maps a collection of User models to UserResponseDtos
    public static List<UserResponseDto> ToResponseDtoList(this List<User> users)
    {
        if (users == null)
            return new List<UserResponseDto>();

        return users.Select(u => u.ToResponseDto()).ToList();
    }

    /// Maps a CreateUserDto to a User model
    public static User ToUserModel(this CreateUserDto dto)
    {
        if (dto == null)
            return null;

        return new User
        {
            Name = dto.Name,
            Username = dto.Username,
            Email = dto.Email
        };
    }

    /// Updates a User model from an UpdateUserDto
    public static void UpdateFromDto(this User user, UpdateUserDto dto)
    {
        if (dto == null)
            return;

        user.Name = dto.Name;
        user.Username = dto.Username;
        user.Email = dto.Email;
    }
}
