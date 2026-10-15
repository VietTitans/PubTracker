using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecordData;
using RecordService.BusinessLogic.UsersService;
using RecordService.DTOs;
using RecordService.DTOs.UserDto;
using RecordService.Exceptions;
using RecordService.Extensions;
using System.Security.Claims;

namespace RecordService.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IUsersService _userService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUsersService userService, ILogger<UsersController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        try
        {
            var user = await _userService.GetUserByIdAsync(int.Parse(userId));

            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            return Ok(user.ToResponseDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current user");
            return StatusCode(500, new { message = "Error retrieving user" });
        }
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        if (!User.IsInRole("Admin") && int.Parse(userId) != id)
        {
            return Forbid();
        }

        try
        {
            var user = await _userService.GetUserByIdAsync(id);

            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            return Ok(user.ToResponseDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId}", id);
            return StatusCode(500, new { message = "Error retrieving user" });
        }
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] PageRequest pageRequest)
    {
        try
        {
            var users = await _userService.GetUsersPageAsync(pageRequest.Page, pageRequest.PageSize);
            return Ok(users.ToPagedResponseDto(pageRequest));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users");
            return StatusCode(500, new { message = "Error retrieving users" });
        }
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<IActionResult> UpdateCurrentUser([FromBody] UpdateUserDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        try
        {
            var user = new User
            {
                Id = int.Parse(userId),
                Name = dto.Name,
                Username = dto.Username,
                Email = dto.Email
            };

            await _userService.UpdateUserAsync(int.Parse(userId), user, User.FindFirstValue("sub"));

            var updatedUser = await _userService.GetUserByIdAsync(int.Parse(userId));
            return Ok(updatedUser.ToResponseDto());
        }
        catch (EmailAlreadyInUseException ex)
        {
            return StatusCode(409, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", userId);
            return StatusCode(500, new { message = "Error updating user" });
        }
    }

    [Authorize]
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        try
        {
            await _userService.SoftDeleteUserAsync(int.Parse(userId));
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId}", userId);
            return StatusCode(500, new { message = "Error deleting user" });
        }
    }

    [Authorize]
    [HttpGet("{userId}/search-queries")]
    public async Task<IActionResult> GetUserSearchQueries(int userId, [FromQuery] PageRequest pageRequest)
    {
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (callerId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        if (!User.IsInRole("Admin") && int.Parse(callerId) != userId)
        {
            return Forbid();
        }

        try
        {
            var queries = await _userService.GetSearchQueriesPageByUserAsync(userId, pageRequest.Page, pageRequest.PageSize);
            return Ok(queries.ToPagedResponseDto(pageRequest));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving search queries for user {UserId}", userId);
            return StatusCode(500, new { message = "Error retrieving search queries" });
        }
    }

    [Authorize]
    [HttpGet("me/search-queries")]
    public async Task<IActionResult> GetCurrentUserSearchQueries([FromQuery] PageRequest pageRequest)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        try
        {
            var queries = await _userService.GetSearchQueriesPageByUserAsync(int.Parse(userId), pageRequest.Page, pageRequest.PageSize);
            return Ok(queries.ToPagedResponseDto(pageRequest));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving search queries for user {UserId}", userId);
            return StatusCode(500, new { message = "Error retrieving search queries" });
        }
    }
}
