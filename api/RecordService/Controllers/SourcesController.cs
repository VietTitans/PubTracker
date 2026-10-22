using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecordService.BusinessLogic.SourcesService;
using RecordService.DTOs;
using RecordService.Extensions;

namespace RecordService.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
public class SourcesController : ControllerBase
{
    private readonly ISourcesService _sourcesService;
    private readonly ILogger<SourcesController> _logger;

    public SourcesController(ISourcesService sourcesService, ILogger<SourcesController> logger)
    {
        _sourcesService = sourcesService;
        _logger = logger;
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
            _logger.LogError(ex, "Error retrieving source {SourceId}", id);
            return StatusCode(500, new { message = "Error retrieving source" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetSources([FromQuery] PageRequest pageRequest)
    {
        try
        {
            var sources = await _sourcesService.GetSourcesPageAsync(pageRequest.Page, pageRequest.PageSize);
            return Ok(sources.ToPagedResponseDto(pageRequest));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving sources");
            return StatusCode(500, new { message = "Error retrieving sources" });
        }
    }
}
