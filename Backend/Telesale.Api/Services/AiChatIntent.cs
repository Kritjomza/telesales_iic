namespace Telesale.Api.Services;

public enum AiChatIntent
{
    CustomerProfile,
    CustomerMissingFields,
    CustomerNearExpiry,
    GlobalNearExpiry,
    CustomerPhone,
    CustomerEmail,
    Unknown
}

public enum AiChatToolAction
{
    SearchCustomer,
    GetCustomerProfile,
    GetMissingFields,
    GetNearExpiryCustomers,
    GetCustomerExpiry,
    ClarifyQuestion
}

public enum AiChatSortBy
{
    ExpiryDate,
    RenewalDays
}
