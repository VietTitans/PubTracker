using RecordData;
using RecordService.DTOs;
using RecordService.DTOs.UserDto;

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

    /// Maps a page of User models to a paged response of UserResponseDtos
    public static PagedResponseDto<UserResponseDto> ToPagedResponseDto(this Paged<User> users, PageRequest pageRequest)
    {
        return users.ToPagedResponseDto(pageRequest, u => u.ToResponseDto());
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
