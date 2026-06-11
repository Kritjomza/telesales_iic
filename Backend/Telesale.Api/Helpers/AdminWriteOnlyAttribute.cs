using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace Telesale.Api.Helpers;

public class AdminWriteOnlyAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (method != "GET" && method != "HEAD" && method != "OPTIONS")
        {
            var user = context.HttpContext.User;
            var role = AppRoles.Normalize(user.FindFirst(ClaimTypes.Role)?.Value);
            if (!AppRoles.IsAdminRole(role))
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
