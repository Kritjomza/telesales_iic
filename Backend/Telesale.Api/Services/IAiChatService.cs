using System.Security.Claims;
using Telesale.Api.Models;

namespace Telesale.Api.Services;

public interface IAiChatService
{
    Task<AiChatResponseDto> SendMessageAsync(
        string message,
        uint? contextCustomerId,
        uint? lastSelectedCustomerId,
        string? lastSelectedCustomerName,
        ClaimsPrincipal user,
        CancellationToken cancellationToken);
}
