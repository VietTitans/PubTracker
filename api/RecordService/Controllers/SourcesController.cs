using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecordService.BusinessLogic;
using RecordService.Extensions;

namespace RecordService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SourcesController : ControllerBase
{
    private readonly ISourcesService _sourcesService;

    public SourcesController(ISourcesService sourcesService)
    {
        _sourcesService = sourcesService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllSources()
    {
        try
        {
            var sources = await _sourcesService.GetAllSourcesAsync();
            return Ok(sources.ToResponseDtoList());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving sources", error = ex.Message });
        }
    }
}
