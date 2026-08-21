using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpGet("{id}")]
    public async Task<IActionResult> GetSourceById(int id)
    {
        try
        {
            var source = await _sourcesService.GetSourceByIdAsync(id);
            if (source == null)
            {
                return NotFound();
            }
            return Ok(source.ToResponseDto());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving source", error = ex.Message });
        }
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
