using System.Security.Claims;

namespace Telesale.Api.Services;

public interface ICustomerContextService
{
    Task<CustomerContextResult> GetCustomerContextAsync(
        string message,
        uint? contextCustomerId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken);
}

public sealed record CustomerContextResult(IReadOnlyList<CustomerContextCustomer> Matches);

public sealed record CustomerContextCustomer(
    uint Id,
    string CompanyName,
    string? Phone,
    string? Address,
    string? Status,
    string? BusinessType,
    DateTime? UpdatedAt,
    IReadOnlyList<CustomerContextContact> Contacts,
    IReadOnlyList<string> MissingFields);

public sealed record CustomerContextContact(
    string? Name,
    string? Phone,
    string? Email);
