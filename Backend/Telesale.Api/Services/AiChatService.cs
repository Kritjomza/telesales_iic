using System.Security.Claims;
using System.Text;
using Telesale.Api.Models;

namespace Telesale.Api.Services;

public sealed class AiChatService : IAiChatService
{
    private readonly ICustomerContextService _customerContextService;

    public AiChatService(ICustomerContextService customerContextService)
    {
        _customerContextService = customerContextService;
    }

    public async Task<AiChatResponseDto> SendMessageAsync(
        string message,
        uint? contextCustomerId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var context = await _customerContextService.GetCustomerContextAsync(
            message,
            contextCustomerId,
            user,
            cancellationToken);

        return new AiChatResponseDto
        {
            Reply = BuildReply(context),
            Metadata = new AiChatMetadataDto
            {
                Source = "database",
                UsedAi = false,
                MatchedCustomersCount = context.Matches.Count
            }
        };
    }

    private static string BuildReply(CustomerContextResult context)
    {
        if (context.Matches.Count == 0)
        {
            return "No matching customer was found in the database.";
        }

        if (context.Matches.Count > 1)
        {
            var builder = new StringBuilder("Multiple customers matched. Please specify one of these customers:");
            foreach (var customer in context.Matches)
            {
                builder.AppendLine();
                builder.Append("- ");
                builder.Append(customer.CompanyName);
                builder.Append(" (ID: ");
                builder.Append(customer.Id);
                builder.Append(')');
            }

            return builder.ToString();
        }

        return FormatCustomer(context.Matches[0]);
    }

    private static string FormatCustomer(CustomerContextCustomer customer)
    {
        var primaryContact = customer.Contacts.FirstOrDefault();
        var builder = new StringBuilder();
        builder.AppendLine($"Customer ID: {customer.Id}");
        builder.AppendLine($"Company: {customer.CompanyName}");
        builder.AppendLine($"Phone: {ValueOrUnavailable(customer.Phone)}");
        builder.AppendLine($"Address: {ValueOrUnavailable(customer.Address)}");
        builder.AppendLine($"Status: {ValueOrUnavailable(customer.Status)}");
        builder.AppendLine($"Business type: {ValueOrUnavailable(customer.BusinessType)}");
        builder.AppendLine($"Contact name: {ValueOrUnavailable(primaryContact?.Name)}");
        builder.AppendLine($"Contact phone: {ValueOrUnavailable(primaryContact?.Phone)}");
        builder.AppendLine($"Contact email: {ValueOrUnavailable(primaryContact?.Email)}");
        builder.Append($"Updated at: {customer.UpdatedAt?.ToString("yyyy-MM-dd") ?? "not available"}");

        if (customer.MissingFields.Count > 0)
        {
            builder.AppendLine();
            builder.Append("Missing data: ");
            builder.Append(string.Join(", ", customer.MissingFields));
        }

        return builder.ToString();
    }

    private static string ValueOrUnavailable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "not available" : value;
    }
}
