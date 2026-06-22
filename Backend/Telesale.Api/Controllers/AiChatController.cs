using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telesale.Api.Models;

namespace Telesale.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ai-chat")]
public class AiChatController : ControllerBase
{
    private const int MaxMessageLength = 500;
    private const string MockReply = "AI Chat Assistant endpoint is ready. Customer context retrieval will be added in Sprint 2.";

    [HttpPost]
    public IActionResult SendMessage([FromBody] AiChatRequestDto? request)
    {
        var message = request?.Message?.Trim();

        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest(new { message = "Message is required." });
        }

        if (message.Length > MaxMessageLength)
        {
            return BadRequest(new { message = "Message must be 500 characters or fewer." });
        }

        return Ok(new AiChatResponseDto
        {
            Reply = MockReply
        });
    }
}
