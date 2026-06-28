using System.Security.Claims;

namespace Telesale.Api.Services;

public interface ICustomerContextService
{
    Task<CustomerContextResult> GetCustomerContextAsync(
        CustomerContextRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken);
}

public sealed record CustomerContextRequest(
    string Message,
    AiChatIntent Intent,
    AiChatToolAction ToolAction,
    string? CompanyKeyword,
    uint? ContextCustomerId,
    string? ContextCustomerName,
    bool NeedsGlobalSearch,
    int? Limit,
    AiChatSortBy? SortBy);

public sealed record CustomerContextResult(
    IReadOnlyList<CustomerContextCustomer> Matches,
    bool IsGlobalNearExpiry = false,
    bool ExpiryFieldSupported = true,
    bool UsedNearestUpcomingFallback = false,
    int NearExpiryWindowDays = 30);

public sealed record CustomerContextCustomer(
    uint Id,
    string CompanyName,
    string? Phone,
    string? Address,
    string? Status,
    string? BusinessType,
    DateTime? UpdatedAt,
    DateOnly? ExpiryDate,
    int? RenewalDays,
    IReadOnlyList<CustomerContextContact> Contacts,
    IReadOnlyList<string> MissingFields);

public sealed record CustomerContextContact(
    string? Name,
    string? Phone,
    string? Email);
