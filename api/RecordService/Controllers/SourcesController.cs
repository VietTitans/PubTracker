using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RecordService.DataAccess;

namespace RecordService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SourcesController : ControllerBase
{
    private readonly SourcesDataAccess _dataAccess;

    public SourcesController(SourcesDataAccess dataAccess)
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
