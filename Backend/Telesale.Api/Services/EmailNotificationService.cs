using Microsoft.Extensions.Logging;

namespace Telesale.Api.Services;

public class EmailNotificationService : IEmailNotificationService
{
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(ILogger<EmailNotificationService> logger)
    {
        _logger = logger;
    }

    public Task NotifyAdminEditAsync(string adminUsername, string actionName, string entityType, string entityId, string details)
    {
        // Preparation hook for email notification
        _logger.LogWarning("EMAIL NOTIFICATION HOOK: Admin '{AdminUsername}' edited {EntityType} (ID: {EntityId}) via {ActionName}. Details: {Details}",
            adminUsername, entityType, entityId, actionName, details);
        
        return Task.CompletedTask;
    }
}
