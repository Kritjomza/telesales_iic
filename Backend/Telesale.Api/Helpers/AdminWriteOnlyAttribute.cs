using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using System;

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
            
            if (role == AppRoles.SuperAdmin)
            {
                return;
            }

            var path = context.HttpContext.Request.Path.Value ?? "";
            var tableType = "";
            if (path.Contains("/brands", StringComparison.OrdinalIgnoreCase)) tableType = "brands";
            else if (path.Contains("/products", StringComparison.OrdinalIgnoreCase)) tableType = "products";
            else if (path.Contains("/antivirus-prices", StringComparison.OrdinalIgnoreCase)) tableType = "products";
            else if (path.Contains("/competitors", StringComparison.OrdinalIgnoreCase)) tableType = "competitors";
            else if (path.Contains("/business-types", StringComparison.OrdinalIgnoreCase)) tableType = "businesstypes";
            else if (path.Contains("/categories", StringComparison.OrdinalIgnoreCase)) tableType = "categories";
            else if (path.Contains("/profiles", StringComparison.OrdinalIgnoreCase)) tableType = "profiles";

            var isDelete = string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase);

            if (isDelete)
            {
                var allowed = false;
                if (role == AppRoles.Manager)
                {
                    allowed = tableType == "brands" || tableType == "products" || tableType == "competitors" || tableType == "businesstypes" || tableType == "profiles";
                }
                
                if (!allowed)
                {
                    context.Result = new ForbidResult();
                }
            }
            else
            {
                var allowed = false;
                if (role == AppRoles.Admin)
                {
                    allowed = true;
                }
                else if (role == AppRoles.Manager)
                {
                    allowed = tableType == "brands" || tableType == "products" || tableType == "competitors" || tableType == "businesstypes" || tableType == "profiles";
                }

                if (!allowed)
                {
                    context.Result = new ForbidResult();
                }
            }
        }
    }
}
