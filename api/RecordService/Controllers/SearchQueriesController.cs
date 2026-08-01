using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecordService.BusinessLogic;
using RecordService.Dtos;
using RecordService.Extensions;

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
}
