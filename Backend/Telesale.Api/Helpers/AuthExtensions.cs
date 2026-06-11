using System.Security.Claims;

namespace Telesale.Api.Helpers;

public static class AuthExtensions
{
    public static uint? GetUserId(this ClaimsPrincipal user)
    {
        var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return uint.TryParse(idStr, out var id) ? id : null;
    }

    public static string GetUserRole(this ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    }

    public static string GetUserPosition(this ClaimsPrincipal user)
    {
        return user.FindFirstValue("Position") ?? string.Empty;
    }

    public static bool IsAdmin(this ClaimsPrincipal user)
    {
        var role = user.GetUserRole();
        return role == "Admin" || role == "Super Admin";
    }

    public static bool IsSupervisor(this ClaimsPrincipal user)
    {
        var role = user.GetUserRole();
        return role == "Manager";
    }

    public static bool IsAgent(this ClaimsPrincipal user)
    {
        var role = user.GetUserRole();
        return role == "Sale" || role == "Tele Sale" || role == "Tele sale";
    }

    public static bool IsViewer(this ClaimsPrincipal user)
    {
        // Add if any Viewer role is needed, or keep it false for now
        var role = user.GetUserRole();
        return role == "Viewer";
    }
}
