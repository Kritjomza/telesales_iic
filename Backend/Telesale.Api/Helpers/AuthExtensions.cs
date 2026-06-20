using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;
using Telesale.Api.Models;

namespace Telesale.Api.Helpers;

public static class AppRoles
{
    public const string SuperAdmin = "Super Admin";
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Supervisor = "Supervisor";
    public const string Sale = "Sale";
    public const string TeleSale = "Tele Sale";
    public const string Viewer = "Viewer";

    public static string Normalize(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return string.Empty;

        return role.Trim().ToLowerInvariant() switch
        {
            "super admin" => SuperAdmin,
            "admin" => Admin,
            "manager" => Manager,
            "supervisor" => Supervisor,
            "sale" => Sale,
            "tele sale" => TeleSale,
            "telesale" => TeleSale,
            "tele-sale" => TeleSale,
            "viewer" => Viewer,
            _ => role.Trim()
        };
    }

    public static bool IsAdminRole(string role) => role == Admin || role == SuperAdmin;
    public static bool IsSupervisorRole(string role) => role == Manager || role == Supervisor;
    public static bool IsAgentRole(string role) => role == Sale || role == TeleSale;
}

public static class AuthExtensions
{
    public static uint? GetUserId(this ClaimsPrincipal user)
    {
        var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return uint.TryParse(idStr, out var id) ? id : null;
    }

    public static string GetUserRole(this ClaimsPrincipal user)
    {
        return AppRoles.Normalize(user.FindFirstValue(ClaimTypes.Role));
    }

    public static string GetUserPosition(this ClaimsPrincipal user)
    {
        return user.FindFirstValue("Position") ?? string.Empty;
    }

    public static bool IsAdmin(this ClaimsPrincipal user)
    {
        return AppRoles.IsAdminRole(user.GetUserRole());
    }

    public static bool IsSupervisor(this ClaimsPrincipal user)
    {
        return AppRoles.IsSupervisorRole(user.GetUserRole());
    }

    public static bool IsAgent(this ClaimsPrincipal user)
    {
        return AppRoles.IsAgentRole(user.GetUserRole());
    }

    public static bool IsViewer(this ClaimsPrincipal user)
    {
        return user.GetUserRole() == AppRoles.Viewer;
    }

    public static bool CanReadManagementData(this ClaimsPrincipal user)
    {
        var role = user.GetUserRole();
        return AppRoles.IsAdminRole(role) || AppRoles.IsSupervisorRole(role) || AppRoles.IsAgentRole(role) || role == AppRoles.Viewer;
    }

    public static bool CanReadReports(this ClaimsPrincipal user)
    {
        var role = user.GetUserRole();
        return AppRoles.IsAdminRole(role) || AppRoles.IsSupervisorRole(role) || role == AppRoles.Viewer;
    }

    public static bool CanManageAssignments(this ClaimsPrincipal user)
    {
        var role = user.GetUserRole();
        return AppRoles.IsAdminRole(role) || AppRoles.IsSupervisorRole(role);
    }

    public static bool CanWriteCustomerWorkflow(this ClaimsPrincipal user)
    {
        var role = user.GetUserRole();
        return role != string.Empty && role != AppRoles.Viewer;
    }

    public static IQueryable<customer> ApplyCustomerScope(
        this IQueryable<customer> query,
        ClaimsPrincipal user,
        TelesaleDbContext db)
    {
        var userId = user.GetUserId();
        var role = user.GetUserRole();
        if (userId == null || string.IsNullOrEmpty(role))
        {
            return query.Where(_ => false);
        }

        if (AppRoles.IsAdminRole(role) || role == AppRoles.Viewer || AppRoles.IsSupervisorRole(role) || role == AppRoles.Sale || role == AppRoles.TeleSale)
        {
            return query;
        }

        return query.Where(_ => false);
    }

    public static async Task<bool> HasCustomerAccessAsync(
        this ClaimsPrincipal user,
        customer c,
        TelesaleDbContext db,
        CancellationToken cancellationToken = default)
    {
        var userId = user.GetUserId();
        var role = user.GetUserRole();
        if (userId == null || string.IsNullOrEmpty(role)) return false;

        if (AppRoles.IsAdminRole(role) || role == AppRoles.Viewer || AppRoles.IsSupervisorRole(role) || role == AppRoles.Sale || role == AppRoles.TeleSale)
        {
            return true;
        }

        return false;
    }
}
