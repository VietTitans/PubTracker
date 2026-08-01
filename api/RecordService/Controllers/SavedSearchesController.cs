using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecordService.BusinessLogic;
using RecordService.Dtos;
using RecordService.ErrorHandling;
using RecordService.Extensions;
using RecordService.Validation;
using RecordService.Validators;
using System.Security.Claims;

namespace RecordService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SavedSearchesController : ControllerBase
{
    private readonly ISavedSearchesService _savedSearchesService;
    private readonly IErrorHandler _errorHandler;
    private readonly IValidator<SavedSearchCreateDto> _createValidator;

    public SavedSearchesController(
        ISavedSearchesService savedSearchesService,
        IErrorHandler errorHandler)
    {
        _savedSearchesService = savedSearchesService;
        _errorHandler = errorHandler;
        _createValidator = new SavedSearchCreateDtoValidator();
    }

    //[Authorize(Policy = "UserOrAdmin")]
    [HttpGet]
    public async Task<IActionResult> GetSavedSearches([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return _errorHandler.Unauthorized("User ID not found in claims.");
        }

        try
        {
            var savedSearches = await _savedSearchesService.GetSavedSearchesByUserAsync(int.Parse(userId), page, pageSize);
            return Ok(savedSearches.ToResponseDtoList());
        }
        catch (Exception ex)
        {
            return _errorHandler.InternalServerError("Error retrieving saved searches", ex.Message);
        }
    }

    //[Authorize(Policy = "UserOrAdmin")]
    [HttpGet("{savedSearchId}")]
    public async Task<IActionResult> GetSavedSearchById(int savedSearchId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return _errorHandler.Unauthorized("User ID not found in claims.");
        }

        try
        {
            var savedSearch = await _savedSearchesService.GetSavedSearchByIdAsync(savedSearchId);
            if (savedSearch == null)
            {
                return _errorHandler.NotFound("Saved search not found.");
            }

            if (savedSearch.UserId != int.Parse(userId))
            {
                return _errorHandler.Forbidden("You do not have permission to access this saved search.");
            }

            return Ok(savedSearch.ToResponseDto());
        }
        catch (Exception ex)
        {
            return _errorHandler.InternalServerError("Error retrieving saved search", ex.Message);
        }
    }

    //[Authorize(Policy = "UserOrAdmin")]
    [HttpGet("{savedSearchId}/records")]
    public async Task<IActionResult> GetSavedSearchRecords(int savedSearchId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        try
        {
            var savedSearch = await _savedSearchesService.GetSavedSearchByIdAsync(savedSearchId);
            if (savedSearch == null)
            {
                return NotFound(new { message = "Saved search not found." });
            }

            if (savedSearch.UserId != int.Parse(userId))
            {
                return Forbid();
            }

            // TODO: Implement actual record retrieval for this saved search
            return Ok(new { message = "Records endpoint returning placeholder data" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error retrieving records", error = ex.Message });
        }
    }

    //[Authorize(Policy = "UserOrAdmin")]
    [HttpPost]
    public async Task<IActionResult> CreateSavedSearch([FromBody] SavedSearchCreateDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return _errorHandler.Unauthorized("User ID not found in claims.");
        }

        // Validate input using IValidator pattern
        var validationResult = _createValidator.Validate(dto);
        if (!validationResult.IsValid)
        {
            return _errorHandler.BadRequest(string.Join(", ", validationResult.Errors));
        }

        try
        {
            var savedSearch = await _savedSearchesService.CreateSavedSearchAsync(int.Parse(userId), dto.SearchQueryId);
            return CreatedAtAction(nameof(GetSavedSearchById), new { savedSearchId = savedSearch.Id }, savedSearch.ToResponseDto());
        }
        catch (Exception ex)
        {
            return _errorHandler.InternalServerError("Error creating saved search", ex.Message);
        }
    }

    //[Authorize(Policy = "UserOrAdmin")]
    [HttpPut("{savedSearchId}")]
    public async Task<IActionResult> UpdateSavedSearch(int savedSearchId, [FromBody] SavedSearchCreateDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        try
        {
            var savedSearch = await _savedSearchesService.GetSavedSearchByIdAsync(savedSearchId);
            if (savedSearch == null)
            {
                return NotFound(new { message = "Saved search not found." });
            }

            if (savedSearch.UserId != int.Parse(userId))
            {
                return Forbid();
            }

            var updated = await _savedSearchesService.UpdateSavedSearchAsync(savedSearchId, dto.SearchQueryId);
            return Ok(updated.ToResponseDto());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error updating saved search", error = ex.Message });
        }
    }

    //[Authorize(Policy = "UserOrAdmin")]
    [HttpDelete("{savedSearchId}")]
    public async Task<IActionResult> DeleteSavedSearch(int savedSearchId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        try
        {
            var savedSearch = await _savedSearchesService.GetSavedSearchByIdAsync(savedSearchId);
            if (savedSearch == null)
            {
                return NotFound(new { message = "Saved search not found." });
            }

            if (savedSearch.UserId != int.Parse(userId))
            {
                return Forbid();
            }

            await _savedSearchesService.DeleteSavedSearchAsync(savedSearchId);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error deleting saved search", error = ex.Message });
        }
    }
}
