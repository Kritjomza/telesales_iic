using System.Security.Claims;
using Telesale.Api.Models;

namespace Telesale.Api.Services;

public interface IAiChatService
{
    Task<AiChatResponseDto> SendMessageAsync(
        string message,
        uint? contextCustomerId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken);
}
