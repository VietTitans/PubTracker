using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecordService.BusinessLogic.SearchQueriesService;
using RecordService.DTOs;
using RecordService.DTOs.SearchQueryDto;
using RecordService.Exceptions;
using RecordService.Extensions;
using System.Security.Claims;

namespace RecordService.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
public class SearchQueriesController : ControllerBase
{
    private readonly ISearchQueriesService _searchQueriesService;

    private readonly ILogger<SearchQueriesController> _logger;

    public SearchQueriesController(ISearchQueriesService searchQueriesService, ILogger<SearchQueriesController> logger)
    {
        _searchQueriesService = searchQueriesService;
        _logger = logger;
    }

    [Authorize]
    [HttpGet("{searchQueryId}")]
    public async Task<IActionResult> GetSearchQueryById(int searchQueryId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        try
        {
            if (!User.IsInRole("Admin"))
            {
                var subscriberIds = await _searchQueriesService.GetUserSubscribersForQueryAsync(searchQueryId);
                if (!subscriberIds.Contains(int.Parse(userId)))
                {
                    return Forbid();
                }
            }

            var searchQuery = await _searchQueriesService.GetSearchQueryByIdAsync(searchQueryId);
            if (searchQuery == null)
            {
                return NotFound(new { message = "Search query not found." });
            }

            return Ok(searchQuery.ToResponseDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving search query {SearchQueryId}", searchQueryId);
            return StatusCode(500, new { message = "Error retrieving search query" });
        }
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("{searchQueryId}/users")]
    public async Task<IActionResult> GetSearchQueryUsers(int searchQueryId, [FromQuery] PageRequest pageRequest)
    {
        try
        {
            var userIds = await _searchQueriesService.GetUserSubscribersPageForQueryAsync(searchQueryId, pageRequest.Page, pageRequest.PageSize);
            return Ok(userIds.ToPagedResponseDto(pageRequest, id => id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users for search query {SearchQueryId}", searchQueryId);
            return StatusCode(500, new { message = "Error retrieving users for search query" });
        }
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateSearchQuery([FromBody] CreateSearchQueryDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        try
        {
            var searchQuery = await _searchQueriesService.SubscribeAsync(int.Parse(userId), dto.TargetUrl);
            return CreatedAtAction(nameof(GetSearchQueryById), new { searchQueryId = searchQuery.Id }, searchQuery.ToResponseDto());
        }
        catch (AlreadySubscribedException)
        {
            return Conflict(new { message = "Already subscribed to this search query." });
        }
        catch (SubscriptionLimitExceededException ex)
        {
            return UnprocessableEntity(new { message = ex.Message });
        }
        catch (Exception ex) when (ex is InvalidOperationException or UriFormatException or ArgumentException)
        {
            return BadRequest(new { message = "Unsupported or unrecognized search URL." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating search query for user {UserId}", userId);
            return StatusCode(500, new { message = "Error creating search query" });
        }
    }

    [Authorize]
    [HttpDelete("{searchQueryId}")]
    public async Task<IActionResult> UnsubscribeFromSearchQuery(int searchQueryId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        try
        {
            var unsubscribed = await _searchQueriesService.UnsubscribeAsync(int.Parse(userId), searchQueryId);
            if (!unsubscribed)
            {
                return NotFound(new { message = "Subscription not found." });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unsubscribing user {UserId} from search query {SearchQueryId}", userId, searchQueryId);
            return StatusCode(500, new { message = "Error unsubscribing from search query" });
        }
    }
}
