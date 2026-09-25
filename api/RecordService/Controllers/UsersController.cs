using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecordData;
using RecordService.BusinessLogic.UsersService;
using RecordService.DTOs.UserDto;
using RecordService.Extensions;
using System.Security.Claims;

namespace RecordService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IUsersService _userService;

    public UsersController(IUsersService userService)
    {
        _userService = userService;
    }

    [Authorize(Policy = "UserOrAdmin")]
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
            return StatusCode(500, new { message = "Error retrieving user", error = ex.Message });
        }
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
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
            return StatusCode(500, new { message = "Error retrieving user", error = ex.Message });
        }
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        try
        {
            var users = await _userService.GetUsersAsync();
            return Ok(users.ToResponseDtoList());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving users", error = ex.Message });
        }
    }

    // Anonymous by design: this is the sign-up path for someone who doesn't have an account yet,
    // so it can't require [Authorize]. An already-logged-in caller has no reason to hit it.
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return StatusCode(403, new { message = "Already logged in; cannot create a new user." });
        }

        try
        {
            var user = dto.ToUserModel();
            var createdUser = await _userService.CreateUserAsync(user);

            if (createdUser == null)
            {
                return StatusCode(500, new { message = "Error creating user" });
            }

            return CreatedAtAction(nameof(GetUserById), new { id = createdUser.Id }, createdUser.ToResponseDto());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error creating user", error = ex.Message });
        }
    }

    [Authorize(Policy = "UserOrAdmin")]
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

            await _userService.UpdateUserAsync(int.Parse(userId), user);

            var updatedUser = await _userService.GetUserByIdAsync(int.Parse(userId));
            return Ok(updatedUser.ToResponseDto());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error updating user", error = ex.Message });
        }
    }

    [Authorize(Policy = "UserOrAdmin")]
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
            return StatusCode(500, new { message = "Error deleting user", error = ex.Message });
        }
    }

    [Authorize(Policy = "UserOrAdmin")]
    [HttpGet("{userId}/search-queries")]
    public async Task<IActionResult> GetUserSearchQueries(int userId)
    {
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (callerId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        if (!User.IsInRole("Admin") && int.Parse(callerId) != userId)
        {
            return StatusCode(403, new { message = "Cannot view another user's search queries." });
        }

        try
        {
            var queries = await _userService.GetSearchQueriesByUserAsync(userId);
            return Ok(queries.ToResponseDtoList());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving search queries", error = ex.Message });
        }
    }

    [Authorize(Policy = "UserOrAdmin")]
    [HttpGet("me/search-queries")]
    public async Task<IActionResult> GetCurrentUserSearchQueries()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        try
        {
            var queries = await _userService.GetSearchQueriesByUserAsync(int.Parse(userId));
            return Ok(queries.ToResponseDtoList());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving search queries", error = ex.Message });
        }
    }
}
