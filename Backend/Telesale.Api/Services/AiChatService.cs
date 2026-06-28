using System.Security.Claims;
using System.Text;
using Telesale.Api.Models;

namespace Telesale.Api.Services;

public sealed class AiChatService : IAiChatService
{
    private readonly ICustomerContextService _customerContextService;
    private readonly IOpenRouterClient _openRouterClient;
    private readonly AiChatPromptBuilder _promptBuilder;
    private readonly AiIntentRouter _intentRouter;
    private readonly bool _useOpenRouter;

    public AiChatService(ICustomerContextService customerContextService)
        : this(customerContextService, new DisabledOpenRouterClient(), new AiChatPromptBuilder(), false)
    {
    }

    public AiChatService(
        ICustomerContextService customerContextService,
        IOpenRouterClient openRouterClient,
        AiChatPromptBuilder promptBuilder)
        : this(customerContextService, openRouterClient, promptBuilder, true)
    {
    }

    private AiChatService(
        ICustomerContextService customerContextService,
        IOpenRouterClient openRouterClient,
        AiChatPromptBuilder promptBuilder,
        bool useOpenRouter)
    {
        _customerContextService = customerContextService;
        _openRouterClient = openRouterClient;
        _promptBuilder = promptBuilder;
        _useOpenRouter = useOpenRouter;
        _intentRouter = new AiIntentRouter(openRouterClient, promptBuilder, useOpenRouter);
    }

    public async Task<AiChatResponseDto> SendMessageAsync(
        string message,
        uint? contextCustomerId,
        uint? lastSelectedCustomerId,
        string? lastSelectedCustomerName,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var route = await _intentRouter.RouteAsync(
            message,
            contextCustomerId,
            lastSelectedCustomerId,
            lastSelectedCustomerName,
            cancellationToken);

        var context = await _customerContextService.GetCustomerContextAsync(
            new CustomerContextRequest(
                message,
                route.Intent,
                route.ToolAction,
                route.CompanyKeyword,
                route.ContextCustomerId,
                route.ContextCustomerName,
                route.NeedsGlobalSearch,
                route.Limit,
                route.SortBy),
            user,
            cancellationToken);

        if (_useOpenRouter && context.ExpiryFieldSupported)
        {
            var prompt = _promptBuilder.BuildFinalAnswerPrompt(message, context, route);
            var summary = await _openRouterClient.SummarizeAsync(prompt, cancellationToken);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                return CreateResponse(summary, "ai_tool_answer", true, context, route.Intent);
            }

            return CreateResponse(BuildReply(context, route), "database_fallback", false, context, route.Intent);
        }

        return CreateResponse(BuildReply(context, route), "database_fallback", false, context, route.Intent);
    }

    private static AiChatResponseDto CreateResponse(
        string reply,
        string source,
        bool usedAi,
        CustomerContextResult context,
        AiChatIntent intent)
    {
        var selected = context.Matches.Count == 1 ? context.Matches[0] : null;
        return new AiChatResponseDto
        {
            Reply = reply,
            Metadata = new AiChatMetadataDto
            {
                Source = source,
                UsedAi = usedAi,
                MatchedCustomersCount = context.Matches.Count,
                SelectedCustomerId = selected?.Id,
                SelectedCustomerName = selected?.CompanyName,
                Intent = ToIntentText(intent)
            }
        };
    }

    private static string ToIntentText(AiChatIntent intent)
    {
        return intent switch
        {
            AiChatIntent.CustomerProfile => "customer_profile",
            AiChatIntent.CustomerMissingFields => "customer_missing_fields",
            AiChatIntent.CustomerNearExpiry => "customer_near_expiry",
            AiChatIntent.GlobalNearExpiry => "global_near_expiry",
            AiChatIntent.CustomerPhone => "customer_phone",
            AiChatIntent.CustomerEmail => "customer_email",
            _ => "unknown"
        };
    }

    private static string BuildReply(CustomerContextResult context, AiChatRoute route)
    {
        if (!context.ExpiryFieldSupported)
        {
            return "ยังไม่พบ field วันหมดอายุที่รองรับในระบบ";
        }

        if (context.Matches.Count == 0)
        {
            return context.IsGlobalNearExpiry
                ? "ยังไม่พบข้อมูลลูกค้าที่มีวันหมดอายุในระบบ"
                : "ยังไม่พบบริษัทที่ตรงกับคำค้นในระบบ";
        }

        if (context.Matches.Count > 1)
        {
            return FormatCandidates(context.Matches);
        }

        var customer = context.Matches[0];
        return route.Intent switch
        {
            AiChatIntent.CustomerMissingFields => FormatMissingFields(customer),
            AiChatIntent.CustomerNearExpiry => FormatNearExpiry(customer),
            AiChatIntent.GlobalNearExpiry => FormatGlobalNearExpiry(context.Matches),
            AiChatIntent.CustomerPhone => FormatPhone(customer),
            AiChatIntent.CustomerEmail => FormatEmail(customer),
            _ => FormatCustomer(customer)
        };
    }

    private static string FormatCandidates(IReadOnlyList<CustomerContextCustomer> customers)
    {
        var builder = new StringBuilder("พบบริษัทที่อาจตรงกับคำค้นหลายรายการ กรุณาระบุบริษัทที่ต้องการ:");
        foreach (var customer in customers)
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

    private static string FormatCustomer(CustomerContextCustomer customer)
    {
        var primaryContact = customer.Contacts.FirstOrDefault();
        var builder = new StringBuilder();
        builder.AppendLine($"ข้อมูลบริษัท {customer.CompanyName} (ID: {customer.Id})");
        builder.AppendLine($"เบอร์บริษัท: {ValueOrUnavailable(customer.Phone)}");
        builder.AppendLine($"ที่อยู่: {ValueOrUnavailable(customer.Address)}");
        builder.AppendLine($"สถานะ: {ValueOrUnavailable(customer.Status)}");
        builder.AppendLine($"ประเภทธุรกิจ: {ValueOrUnavailable(customer.BusinessType)}");
        builder.AppendLine($"วันหมดอายุ: {customer.ExpiryDate?.ToString("yyyy-MM-dd") ?? "ไม่มีข้อมูลในระบบ"}");
        builder.AppendLine($"จำนวนวันถึงรอบต่ออายุ: {customer.RenewalDays?.ToString() ?? "ไม่มีข้อมูลในระบบ"}");
        builder.AppendLine($"ผู้ติดต่อ: {ValueOrUnavailable(primaryContact?.Name)}");
        builder.AppendLine($"เบอร์ผู้ติดต่อ: {ValueOrUnavailable(primaryContact?.Phone)}");
        builder.AppendLine($"อีเมลผู้ติดต่อ: {ValueOrUnavailable(primaryContact?.Email)}");
        builder.Append($"อัปเดตล่าสุด: {customer.UpdatedAt?.ToString("yyyy-MM-dd") ?? "ไม่มีข้อมูลในระบบ"}");

        if (customer.MissingFields.Count > 0)
        {
            builder.AppendLine();
            builder.Append("ข้อมูลที่ยังขาด: ");
            builder.Append(string.Join(", ", customer.MissingFields));
        }

        return builder.ToString();
    }

    private static string FormatMissingFields(CustomerContextCustomer customer)
    {
        if (customer.MissingFields.Count == 0)
        {
            return $"บริษัท {customer.CompanyName} ไม่พบข้อมูลสำคัญที่ยังขาดในระบบ";
        }

        return $"บริษัท {customer.CompanyName} ยังขาดข้อมูล: {string.Join(", ", customer.MissingFields)}";
    }

    private static string FormatNearExpiry(CustomerContextCustomer customer)
    {
        if (!customer.ExpiryDate.HasValue || !customer.RenewalDays.HasValue)
        {
            return $"บริษัท {customer.CompanyName} ยังไม่มีข้อมูลวันหมดอายุในระบบ";
        }

        return $"บริษัท {customer.CompanyName}\nวันหมดอายุ: {customer.ExpiryDate:yyyy-MM-dd}\nจำนวนวันถึงรอบต่ออายุ: {customer.RenewalDays}";
    }

    private static string FormatGlobalNearExpiry(IReadOnlyList<CustomerContextCustomer> customers)
    {
        var builder = new StringBuilder("ลูกค้าที่ใกล้หมดอายุ:");
        foreach (var customer in customers)
        {
            builder.AppendLine();
            builder.Append("- ");
            builder.Append(customer.CompanyName);
            builder.Append(" (ID: ");
            builder.Append(customer.Id);
            builder.Append(", วันหมดอายุ: ");
            builder.Append(customer.ExpiryDate?.ToString("yyyy-MM-dd") ?? "not available");
            builder.Append(", วันคงเหลือ: ");
            builder.Append(customer.RenewalDays?.ToString() ?? "not available");
            builder.Append(')');
        }

        return builder.ToString();
    }

    private static string FormatPhone(CustomerContextCustomer customer)
    {
        var primaryContact = customer.Contacts.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Phone));
        var builder = new StringBuilder();
        builder.AppendLine($"{customer.CompanyName}");
        builder.AppendLine($"เบอร์บริษัท: {ValueOrUnavailable(customer.Phone)}");
        builder.Append($"เบอร์ผู้ติดต่อ: {ValueOrUnavailable(primaryContact?.Phone)}");
        return builder.ToString();
    }

    private static string FormatEmail(CustomerContextCustomer customer)
    {
        var primaryContact = customer.Contacts.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Email));
        return $"{customer.CompanyName}\nอีเมลผู้ติดต่อ: {ValueOrUnavailable(primaryContact?.Email)}";
    }

    private static string ValueOrUnavailable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "ไม่มีข้อมูลในระบบ" : value;
    }

    private sealed class DisabledOpenRouterClient : IOpenRouterClient
    {
        public Task<string?> InterpretIntentAsync(OpenRouterPrompt prompt, CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(null);
        }

        public Task<string?> SummarizeAsync(OpenRouterPrompt prompt, CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(null);
        }
    }
}
