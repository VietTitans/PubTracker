using Microsoft.AspNetCore.Mvc;
using RecordService.DataAccess;

namespace RecordService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly UsersDataAccess _dataAccess;

    public UsersController(UsersDataAccess dataAccess)
    {
        _dataAccess = dataAccess;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        try
        {
            var users = await _dataAccess.GetUsersAsync();
            return Ok(users);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving users", error = ex.Message });
        }
    }
}
