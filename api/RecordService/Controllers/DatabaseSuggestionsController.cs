using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecordService.BusinessLogic.DatabaseSuggestionsService;
using RecordService.DTOs;
using RecordService.DTOs.DatabaseSuggestionDto;
using RecordService.Extensions;
using System.Security.Claims;

namespace RecordService.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
public class DatabaseSuggestionsController : ControllerBase
{
    private readonly IDatabaseSuggestionsService _suggestionsService;
    private readonly ILogger<DatabaseSuggestionsController> _logger;

    public DatabaseSuggestionsController(IDatabaseSuggestionsService suggestionsService, ILogger<DatabaseSuggestionsController> logger)
    {
        _suggestionsService = suggestionsService;
        _logger = logger;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetSuggestions([FromQuery] PageRequest pageRequest)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        try
        {
            var suggestions = await _suggestionsService.GetSuggestionsPageAsync(int.Parse(userId), pageRequest.Page, pageRequest.PageSize);
            return Ok(suggestions.ToPagedResponseDto(pageRequest));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving database suggestions");
            return StatusCode(500, new { message = "Error retrieving database suggestions" });
        }
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Suggest([FromBody] CreateDatabaseSuggestionDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        try
        {
            var suggestion = await _suggestionsService.SuggestAsync(int.Parse(userId), dto.Name, dto.WebsiteUrl, dto.Reason);
            return Ok(suggestion.ToResponseDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving database suggestion");
            return StatusCode(500, new { message = "Error saving database suggestion" });
        }
    }

    [Authorize]
    [HttpPut("{suggestionId}/vote")]
    public async Task<IActionResult> AddVote(int suggestionId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        try
        {
            return await _suggestionsService.AddVoteAsync(int.Parse(userId), suggestionId)
                ? NoContent()
                : NotFound(new { message = "Suggestion not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error voting for suggestion {SuggestionId}", suggestionId);
            return StatusCode(500, new { message = "Error saving vote" });
        }
    }

    [Authorize]
    [HttpDelete("{suggestionId}/vote")]
    public async Task<IActionResult> RemoveVote(int suggestionId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        try
        {
            await _suggestionsService.RemoveVoteAsync(int.Parse(userId), suggestionId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing vote for suggestion {SuggestionId}", suggestionId);
            return StatusCode(500, new { message = "Error removing vote" });
        }
    }
}
