using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using System.Threading.Tasks;
using System;
using Telesale.Api.Services;

namespace Telesale.Api.Helpers;

public class NotifyAdminEditAttribute : ActionFilterAttribute
{
    public override async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var resultContext = await next();
        
        var response = resultContext.HttpContext.Response;
        if (response.StatusCode >= 200 && response.StatusCode < 300)
        {
            var request = resultContext.HttpContext.Request;
            var method = request.Method;
            if (method == "POST" || method == "PUT")
            {
                var user = resultContext.HttpContext.User;
                var role = AppRoles.Normalize(user.FindFirst(ClaimTypes.Role)?.Value);
                if (role == AppRoles.Admin)
                {
                    var emailService = resultContext.HttpContext.RequestServices.GetService(typeof(IEmailNotificationService)) as IEmailNotificationService;
                    if (emailService != null)
                    {
                        var adminUsername = user.Identity?.Name ?? "Admin";
                        var actionName = resultContext.ActionDescriptor.DisplayName ?? "Action";
                        var path = request.Path.Value ?? "";
                        
                        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                        var entityType = parts.Length > 1 ? parts[1] : "Unknown";
                        var entityId = parts.Length > 2 ? parts[2] : "New";

                        await emailService.NotifyAdminEditAsync(adminUsername, actionName, entityType, entityId, $"Method: {method}, Path: {path}");
                    }
                }
            }
        }
    }
}
