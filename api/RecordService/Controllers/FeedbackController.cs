using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecordService.BusinessLogic.FeedbackService;
using RecordService.DTOs.FeedbackDto;
using System.Security.Claims;

namespace RecordService.Controllers;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
public class FeedbackController : ControllerBase
{
    private readonly IFeedbackService _feedbackService;
    private readonly ILogger<FeedbackController> _logger;

    public FeedbackController(IFeedbackService feedbackService, ILogger<FeedbackController> logger)
    {
        _feedbackService = feedbackService;
        _logger = logger;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> SubmitFeedback([FromBody] CreateFeedbackDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        try
        {
            await _feedbackService.SubmitFeedbackAsync(int.Parse(userId), dto.Type, dto.Message);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving feedback");
            return StatusCode(500, new { message = "Error saving feedback" });
        }
    }
}
