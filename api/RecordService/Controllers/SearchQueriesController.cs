using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecordService.DTOs.SearchQueryDto;
using RecordService.Exceptions;
using RecordService.Extensions;
using System.Security.Claims;

namespace RecordService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SearchQueriesController : ControllerBase
{
    private readonly ISearchQueriesService _searchQueriesService;

    public SearchQueriesController(ISearchQueriesService searchQueriesService)
    {
        _searchQueriesService = searchQueriesService;
    }

    //[Authorize(Policy = "AdminOnly")]
    [HttpGet("{searchQueryId}")]
    public async Task<IActionResult> GetSearchQueryById(int searchQueryId)
    {
        try
        {
            var searchQuery = await _searchQueriesService.GetSearchQueryByIdAsync(searchQueryId);
            if (searchQuery == null)
            {
                return NotFound(new { message = "Search query not found." });
            }

            return Ok(searchQuery.ToResponseDto());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving search query", error = ex.Message });
        }
    }

    //[Authorize(Policy = "AdminOnly")]
    [HttpGet("{searchQueryId}/users")]
    public async Task<IActionResult> GetSearchQueryUsers(int searchQueryId)
    {
        try
        {
            var userIds = await _searchQueriesService.GetUserSubscribersForQueryAsync(searchQueryId);
            return Ok(new { userIds = userIds });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving users for search query", error = ex.Message });
        }
    }

    //[Authorize(Policy = "UserOrAdmin")]
    [HttpPost]
    public async Task<IActionResult> CreateSearchQuery([FromBody] CreateSearchQueryDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        if (string.IsNullOrWhiteSpace(dto.TargetUrl))
        {
            return BadRequest(new { message = "TargetUrl is required." });
        }

        try
        {
            var searchQuery = await _searchQueriesService.SubscribeAsync(int.Parse(userId), dto.TargetUrl);
            return CreatedAtAction(nameof(GetSearchQueryById), new { searchQueryId = searchQuery.Id }, searchQuery.ToResponseDto());
        }
        catch (AlreadySubscribedException ex)
        {
            return Conflict(new { message = "Already subscribed to this search query.", error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = "Unsupported or unrecognized search URL.", error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error creating search query", error = ex.Message });
        }
    }
}
