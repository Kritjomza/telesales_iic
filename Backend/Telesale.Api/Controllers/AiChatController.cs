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

        if (ContainsBlockedPrompt(message))
        {
            return Ok(new AiChatResponseDto
            {
                Reply = "ไม่สามารถตอบคำขอนี้ได้",
                Metadata = new AiChatMetadataDto
                {
                    Source = "blocked",
                    UsedAi = false,
                    MatchedCustomersCount = 0
                }
            });
        }

        var response = await _aiChatService.SendMessageAsync(
            message,
            request?.ContextCustomerId,
            User,
            cancellationToken);

        return Ok(response);
    }

    private static bool ContainsBlockedPrompt(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();
        return normalized.Contains("system prompt", StringComparison.Ordinal) ||
            normalized.Contains("api key", StringComparison.Ordinal) ||
            normalized.Contains("openrouter_api_key", StringComparison.Ordinal) ||
            normalized.Contains("hidden config", StringComparison.Ordinal) ||
            normalized.Contains("internal rule", StringComparison.Ordinal) ||
            normalized.Contains("database password", StringComparison.Ordinal) ||
            normalized.Contains("db password", StringComparison.Ordinal) ||
            normalized.Contains("credential", StringComparison.Ordinal);
    }
}
