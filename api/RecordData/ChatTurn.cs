namespace RecordService.Models;

/// <summary>
/// One prior turn of a chat conversation, sent by the client so the model has multi-turn
/// context. Role is "user" or "assistant".
/// </summary>
public class ChatTurn
{
    public required string Role { get; set; }
    public required string Text { get; set; }
}
