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
            Username = user.Username,
            Email = user.Email,
            DigestsStopped = user.DigestsStopped
        };
    }

    /// Maps a page of User models to a paged response of UserResponseDtos
    public static PagedResponseDto<UserResponseDto> ToPagedResponseDto(this Paged<User> users, PageRequest pageRequest)
    {
        return users.ToPagedResponseDto(pageRequest, u => u.ToResponseDto());
    }

    /// Updates a User model from an UpdateUserDto
    public static void UpdateFromDto(this User user, UpdateUserDto dto)
    {
        if (dto == null)
            return;

        user.Username = dto.Username;
        user.Email = dto.Email;
    }
}
