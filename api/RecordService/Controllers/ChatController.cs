using Microsoft.AspNetCore.Mvc;
using RecordService.BusinessLogic.ChatService;
using RecordService.DTOs.ChatDto;
using RecordService.Extensions;
using System.Security.Claims;

namespace RecordService.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    //[Authorize(Policy = "UserOrAdmin")]
    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] ChatRequestDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized(new { message = "User ID not found in claims." });
        }

        if (string.IsNullOrWhiteSpace(dto.Question))
        {
            return BadRequest(new { message = "Question is required." });
        }

        try
        {
            var history = dto.History.Select(m => m.ToChatTurn()).ToList();
            var result = await _chatService.AskAsync(int.Parse(userId), dto.Question, history);
            return Ok(result.ToResponseDto());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error answering chat question", error = ex.Message });
        }
    }
}
