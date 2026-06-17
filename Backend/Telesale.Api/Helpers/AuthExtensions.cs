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

        if (AppRoles.IsAdminRole(role) || role == AppRoles.Viewer)
        {
            return query;
        }

        var id = (int)userId.Value;
        if (AppRoles.IsSupervisorRole(role))
        {
            var position = user.GetUserPosition();
            if (string.IsNullOrWhiteSpace(position))
            {
                return query.Where(c => c.owner_id == id);
            }

            return query.Where(c =>
                c.owner_id == id ||
                (c.sale_id != null && db.users.Any(u => u.id == c.sale_id && u.position == position && u.position != null && u.position != "")) ||
                (c.telesale_id != null && db.users.Any(u => u.id == c.telesale_id && u.position == position && u.position != null && u.position != "")));
        }

        if (role == AppRoles.Sale)
        {
            return query.Where(c => c.sale_id == id || c.owner_id == id);
        }

        if (role == AppRoles.TeleSale)
        {
            return query.Where(c => c.telesale_id == id || c.owner_id == id);
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

        if (AppRoles.IsAdminRole(role) || role == AppRoles.Viewer)
        {
            return true;
        }

        var id = (int)userId.Value;
        if (AppRoles.IsSupervisorRole(role))
        {
            if (c.owner_id == id) return true;

            var position = user.GetUserPosition();
            if (string.IsNullOrWhiteSpace(position)) return false;

            return
                (c.sale_id != null && await db.users.AnyAsync(u => u.id == c.sale_id && u.position == position, cancellationToken)) ||
                (c.telesale_id != null && await db.users.AnyAsync(u => u.id == c.telesale_id && u.position == position, cancellationToken));
        }

        return role switch
        {
            AppRoles.Sale => c.sale_id == id || c.owner_id == id,
            AppRoles.TeleSale => c.telesale_id == id || c.owner_id == id,
            _ => false
        };
    }
}
