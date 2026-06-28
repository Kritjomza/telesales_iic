using System.Text.Json;
using System.Text.RegularExpressions;

namespace Telesale.Api.Services;

public sealed class AiIntentRouter
{
    private const int DefaultLimit = 5;
    private const int MaxLimit = 20;
    private readonly IOpenRouterClient _openRouterClient;
    private readonly AiChatPromptBuilder _promptBuilder;
    private readonly bool _useOpenRouter;

    public AiIntentRouter(IOpenRouterClient openRouterClient, AiChatPromptBuilder promptBuilder, bool useOpenRouter)
    {
        _openRouterClient = openRouterClient;
        _promptBuilder = promptBuilder;
        _useOpenRouter = useOpenRouter;
    }

    public async Task<AiChatRoute> RouteAsync(
        string message,
        uint? contextCustomerId,
        uint? lastSelectedCustomerId,
        string? lastSelectedCustomerName,
        CancellationToken cancellationToken)
    {
        var fallback = InterpretDeterministically(message, lastSelectedCustomerName);
        var interpreted = fallback;

        if (_useOpenRouter)
        {
            var prompt = _promptBuilder.BuildIntentPrompt(message, lastSelectedCustomerName);
            var json = await _openRouterClient.InterpretIntentAsync(prompt, cancellationToken);
            interpreted = MergeInterpretations(fallback, TryParseInterpretation(json));
        }

        var hasNewCompany = !string.IsNullOrWhiteSpace(interpreted.CompanyKeyword);
        var useLastCustomer = !hasNewCompany && interpreted.IsFollowUp && lastSelectedCustomerId.HasValue;
        var effectiveCustomerId = contextCustomerId ?? (useLastCustomer ? lastSelectedCustomerId : null);
        var effectiveCustomerName = effectiveCustomerId.HasValue ? lastSelectedCustomerName : null;
        var toolAction = SelectToolAction(interpreted.Intent, interpreted.NeedsGlobalSearch);

        return new AiChatRoute(
            interpreted.Intent,
            toolAction,
            interpreted.CompanyKeyword,
            effectiveCustomerId,
            effectiveCustomerName,
            interpreted.NeedsGlobalSearch,
            interpreted.Limit,
            interpreted.SortBy);
    }

    private static AiChatInterpretation MergeInterpretations(
        AiChatInterpretation fallback,
        AiChatInterpretation? interpreted)
    {
        if (interpreted == null)
        {
            return fallback;
        }

        var companyKeyword = !string.IsNullOrWhiteSpace(interpreted.CompanyKeyword)
            ? interpreted.CompanyKeyword.Trim()
            : fallback.CompanyKeyword;

        var hasNewCompany = !string.IsNullOrWhiteSpace(companyKeyword);

        return new AiChatInterpretation(
            interpreted.Intent == AiChatIntent.Unknown ? fallback.Intent : interpreted.Intent,
            companyKeyword,
            hasNewCompany ? false : interpreted.IsFollowUp || fallback.IsFollowUp,
            interpreted.NeedsGlobalSearch || fallback.NeedsGlobalSearch,
            interpreted.Limit ?? fallback.Limit,
            interpreted.SortBy ?? fallback.SortBy);
    }

    private static AiChatInterpretation InterpretDeterministically(string message, string? lastSelectedCustomerName)
    {
        var normalized = message.Trim().ToLowerInvariant();
        var hasLastCustomer = !string.IsNullOrWhiteSpace(lastSelectedCustomerName);
        var companyKeyword = CustomerContextService.ExtractCompanyKeyword(message);
        var hasCompanyKeyword = HasLikelyCompanyKeyword(message, companyKeyword);
        var hasNearExpiry = ContainsAny(normalized, "หมดอายุ", "ใกล้หมด", "renewal", "expire", "expiry");
        var asksMissing = ContainsAny(normalized, "ข้อมูลที่ยังขาด", "ยังขาด", "missing");
        var asksPhone = ContainsAny(normalized, "เบอร์", "โทร", "phone", "tel");
        var asksEmail = ContainsAny(normalized, "อีเมล", "email", "mail");
        var asksCustomerGroup = ContainsAny(normalized, "ลูกค้า", "บริษัท");
        var isGlobalNearExpiry = hasNearExpiry &&
            (ContainsAny(normalized, "ลูกค้าใกล้หมดอายุ", "บริษัทใกล้หมดอายุ", "ขอบริษัทที่ใกล้หมดอายุ") ||
             (asksCustomerGroup && !hasCompanyKeyword));
        var isFollowUp = hasLastCustomer && !hasCompanyKeyword && !isGlobalNearExpiry;

        if (isGlobalNearExpiry)
        {
            return new AiChatInterpretation(
                AiChatIntent.GlobalNearExpiry,
                null,
                false,
                true,
                ExtractLimit(message),
                AiChatSortBy.RenewalDays);
        }

        var intent = AiChatIntent.CustomerProfile;
        if (hasNearExpiry)
        {
            intent = AiChatIntent.CustomerNearExpiry;
        }
        else if (asksMissing)
        {
            intent = AiChatIntent.CustomerMissingFields;
        }
        else if (asksPhone)
        {
            intent = AiChatIntent.CustomerPhone;
        }
        else if (asksEmail)
        {
            intent = AiChatIntent.CustomerEmail;
        }
        else if (string.IsNullOrWhiteSpace(message))
        {
            intent = AiChatIntent.Unknown;
        }

        return new AiChatInterpretation(
            intent,
            hasCompanyKeyword ? companyKeyword : null,
            isFollowUp,
            false,
            ExtractLimit(message),
            null);
    }

    private static bool HasLikelyCompanyKeyword(string message, string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return false;
        }

        var normalizedKeyword = CustomerContextService.NormalizeCompanyText(keyword);
        if (string.IsNullOrWhiteSpace(normalizedKeyword))
        {
            return false;
        }

        var normalizedMessage = message.Trim().ToLowerInvariant();
        if (ContainsAny(normalizedMessage, "บริษัท", "บริษํท", "บริษัํท", "บจก", "company"))
        {
            return true;
        }

        var nonCompanyQuestion = ContainsAny(normalizedMessage, "ข้อมูลที่ยังขาด", "ยังขาด", "หมดอายุ", "ใกล้หมด", "ลูกค้าใกล้หมดอายุ");
        return !nonCompanyQuestion && normalizedKeyword.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(token => token.Length >= 3);
    }

    private static AiChatToolAction SelectToolAction(AiChatIntent intent, bool needsGlobalSearch)
    {
        if (needsGlobalSearch || intent == AiChatIntent.GlobalNearExpiry)
        {
            return AiChatToolAction.GetNearExpiryCustomers;
        }

        return intent switch
        {
            AiChatIntent.CustomerMissingFields => AiChatToolAction.GetMissingFields,
            AiChatIntent.CustomerNearExpiry => AiChatToolAction.GetCustomerExpiry,
            AiChatIntent.CustomerPhone => AiChatToolAction.GetCustomerProfile,
            AiChatIntent.CustomerEmail => AiChatToolAction.GetCustomerProfile,
            AiChatIntent.CustomerProfile => AiChatToolAction.GetCustomerProfile,
            _ => AiChatToolAction.ClarifyQuestion
        };
    }

    private static AiChatInterpretation? TryParseInterpretation(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var trimmed = json.Trim();
            var start = trimmed.IndexOf('{');
            var end = trimmed.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                return null;
            }

            using var document = JsonDocument.Parse(trimmed[start..(end + 1)]);
            var root = document.RootElement;
            var intentText = root.TryGetProperty("intent", out var intentValue) ? intentValue.GetString() : null;
            var companyKeyword = root.TryGetProperty("companyKeyword", out var keywordValue) && keywordValue.ValueKind != JsonValueKind.Null
                ? keywordValue.GetString()
                : null;
            var isFollowUp = root.TryGetProperty("isFollowUp", out var followUpValue) && followUpValue.ValueKind == JsonValueKind.True;
            var needsGlobalSearch = root.TryGetProperty("needsGlobalSearch", out var globalValue) && globalValue.ValueKind == JsonValueKind.True;
            int? limit = root.TryGetProperty("limit", out var limitValue) && limitValue.TryGetInt32(out var parsedLimit)
                ? ClampLimit(parsedLimit)
                : null;
            var sortBy = root.TryGetProperty("sortBy", out var sortValue) ? ParseSortBy(sortValue.GetString()) : null;

            return new AiChatInterpretation(ParseIntent(intentText), companyKeyword, isFollowUp, needsGlobalSearch, limit, sortBy);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? ExtractLimit(string message)
    {
        var match = Regex.Match(message, @"\d+");
        return match.Success && int.TryParse(match.Value, out var limit) ? ClampLimit(limit) : DefaultLimit;
    }

    private static int ClampLimit(int limit)
    {
        return Math.Clamp(limit, 1, MaxLimit);
    }

    private static AiChatIntent ParseIntent(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "customer_profile" => AiChatIntent.CustomerProfile,
            "customer_missing_fields" => AiChatIntent.CustomerMissingFields,
            "customer_near_expiry" => AiChatIntent.CustomerNearExpiry,
            "global_near_expiry" => AiChatIntent.GlobalNearExpiry,
            "customer_phone" => AiChatIntent.CustomerPhone,
            "customer_email" => AiChatIntent.CustomerEmail,
            _ => AiChatIntent.Unknown
        };
    }

    private static AiChatSortBy? ParseSortBy(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "expirydate" or "expiry_date" => AiChatSortBy.ExpiryDate,
            "renewaldays" or "renewal_days" => AiChatSortBy.RenewalDays,
            _ => null
        };
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.Ordinal));
    }

    private sealed record AiChatInterpretation(
        AiChatIntent Intent,
        string? CompanyKeyword,
        bool IsFollowUp,
        bool NeedsGlobalSearch,
        int? Limit,
        AiChatSortBy? SortBy);
}

public sealed record AiChatRoute(
    AiChatIntent Intent,
    AiChatToolAction ToolAction,
    string? CompanyKeyword,
    uint? ContextCustomerId,
    string? ContextCustomerName,
    bool NeedsGlobalSearch,
    int? Limit,
    AiChatSortBy? SortBy);
