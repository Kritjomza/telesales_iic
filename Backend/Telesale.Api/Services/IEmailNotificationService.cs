namespace Telesale.Api.Services;

public interface IEmailNotificationService
{
    Task NotifyAdminEditAsync(string adminUsername, string actionName, string entityType, string entityId, string details);
}
