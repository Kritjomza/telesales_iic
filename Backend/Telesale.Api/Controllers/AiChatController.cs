using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telesale.Api.Helpers;
using Telesale.Api.Models;
using Telesale.Api.Services;

namespace Telesale.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ai-chat")]
public class AiChatController : ControllerBase
{
    private const int MaxMessageLength = 500;
    private readonly IAiChatService _aiChatService;

    public AiChatController(IAiChatService aiChatService)
    {
        _aiChatService = aiChatService;
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage(
        [FromBody] AiChatRequestDto? request,
        CancellationToken cancellationToken)
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

        if (!User.CanReadManagementData())
        {
            return Forbid();
        }

        var response = await _aiChatService.SendMessageAsync(
            message,
            request?.ContextCustomerId,
            User,
            cancellationToken);

        return Ok(response);
    }
}
