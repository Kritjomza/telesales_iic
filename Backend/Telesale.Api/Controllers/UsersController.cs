using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;
using Telesale.Api.Helpers;
using Telesale.Api.Models;

namespace Telesale.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private static readonly HashSet<string> SupportedRoles =
    [
        AppRoles.Admin, AppRoles.SuperAdmin, AppRoles.Manager, AppRoles.TeleSale, AppRoles.Sale
    ];
    private readonly TelesaleDbContext _db;
    public UsersController(TelesaleDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        if (!User.CanManageAssignments()) return Forbid();
        var position = User.GetUserPosition();
        IQueryable<user> query = _db.users.AsNoTracking().Where(u => u.is_active == null || u.is_active == true);
        if (User.IsSupervisor()) query = query.Where(u => u.position == position && !string.IsNullOrEmpty(position));
        return Ok(await query.Select(u => new UserResponse(u.id, u.name, u.username, u.email, u.roles,
            u.tel ?? "", u.position ?? "", u.is_active != false)).ToListAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsAdmin()) return Forbid();
        Normalize(request);
        var error = Validate(request, true);
        if (error != null) return BadRequest(new { message = error });
        if (User.GetUserRole() == AppRoles.Admin && request.Role == AppRoles.SuperAdmin) return Forbid();
        if (await _db.users.AnyAsync(u => u.username == request.Username, cancellationToken))
            return Conflict(new { message = "Username already exists." });
        if (await _db.users.AnyAsync(u => u.email == request.Email, cancellationToken))
            return Conflict(new { message = "Email already exists." });
        var now = DateTime.UtcNow;
        var entity = new user { name = request.Name, username = request.Username, email = request.Email,
            roles = request.Role, password = BCrypt.Net.BCrypt.HashPassword(request.Password!),
            tel = Clean(request.Tel), position = Clean(request.Position), is_active = request.IsActive,
            created_at = now, updated_at = now };
        _db.users.Add(entity);
        try { await _db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (IsUsersUniqueViolation(ex))
        { return Conflict(new { message = "Username or email already exists." }); }
        return CreatedAtAction(nameof(GetUsers), new { id = entity.id }, ToResponse(entity));
    }

    [HttpPut("{id:uint}")]
    public async Task<IActionResult> UpdateUser(uint id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsAdmin()) return Forbid();
        var target = await _db.users.FirstOrDefaultAsync(u => u.id == id, cancellationToken);
        if (target == null) return NotFound(new { message = "User not found." });
        Normalize(request);
        var error = Validate(request, false);
        if (error != null) return BadRequest(new { message = error });
        if (User.GetUserRole() == AppRoles.Admin)
        {
            if (target.roles == AppRoles.SuperAdmin || request.Role == AppRoles.SuperAdmin) return Forbid();
            if (User.GetUserId() == id && request.Role != target.roles) return Forbid();
        }
        if (await _db.users.AnyAsync(u => u.id != id && u.username == request.Username, cancellationToken))
            return Conflict(new { message = "Username already exists." });
        if (await _db.users.AnyAsync(u => u.id != id && u.email == request.Email, cancellationToken))
            return Conflict(new { message = "Email already exists." });
        target.name = request.Name; target.username = request.Username; target.email = request.Email;
        target.roles = request.Role; target.tel = Clean(request.Tel); target.position = Clean(request.Position);
        target.is_active = request.IsActive; target.updated_at = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Password)) target.password = BCrypt.Net.BCrypt.HashPassword(request.Password);
        try { await _db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (IsUsersUniqueViolation(ex))
        { return Conflict(new { message = "Username or email already exists." }); }
        return Ok(ToResponse(target));
    }

    [HttpDelete("{id:uint}")]
    public async Task<IActionResult> DeleteUser(uint id, CancellationToken cancellationToken)
    {
        if (!User.IsAdmin()) return Forbid();
        var target = await _db.users.FirstOrDefaultAsync(u => u.id == id, cancellationToken);
        if (target == null) return NotFound(new { message = "User not found." });
        if (User.GetUserRole() == AppRoles.Admin && target.roles == AppRoles.SuperAdmin) return Forbid();
        if (User.GetUserId() == id) return Conflict(new { message = "You cannot delete your own user account." });
        var userId = checked((int)id);
        // Protected references discovered across EF mappings, models, schema SQL, and controller usage.
        var referenced = await _db.customers.AnyAsync(c => c.sale_id == userId || c.telesale_id == userId ||
                c.sale_id_bak == userId || c.telesale_id_bak == userId, cancellationToken)
            || await _db.targets.AnyAsync(t => t.user_id == userId, cancellationToken)
            || await _db.import_sessions.AnyAsync(s => s.imported_by == id, cancellationToken)
            || await _db.assignment_histories.AnyAsync(h => h.old_sale_id == userId || h.new_sale_id == userId ||
                h.old_telesale_id == userId || h.new_telesale_id == userId, cancellationToken);
        if (referenced) return Conflict(new { message = "User cannot be deleted because protected business records reference it." });
        _db.users.Remove(target);
        await _db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static UserResponse ToResponse(user u) => new(u.id, u.name, u.username, u.email, u.roles,
        u.tel ?? "", u.position ?? "", u.is_active != false);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void Normalize(UserRequest request)
    {
        request.Name = request.Name?.Trim() ?? ""; request.Username = request.Username?.Trim() ?? "";
        request.Email = request.Email?.Trim() ?? ""; request.Role = AppRoles.Normalize(request.Role);
        request.Tel = request.Tel?.Trim(); request.Position = request.Position?.Trim();
    }
    private static string? Validate(UserRequest request, bool passwordRequired)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Name is required.";
        if (string.IsNullOrWhiteSpace(request.Username)) return "Username is required.";
        if (string.IsNullOrWhiteSpace(request.Email)) return "Email is required.";
        if (request.Name.Length > 255 || request.Username.Length > 255 || request.Email.Length > 255 ||
            request.Tel?.Length > 255 || request.Position?.Length > 255) return "User fields must not exceed 255 characters.";
        if (!new EmailAddressAttribute().IsValid(request.Email)) return "Email is invalid.";
        if (!SupportedRoles.Contains(request.Role)) return "Role is invalid.";
        if (passwordRequired && string.IsNullOrWhiteSpace(request.Password)) return "Password is required.";
        if (!string.IsNullOrWhiteSpace(request.Password) && (request.Password.Length < 8 ||
            !request.Password.Any(char.IsUpper) || !request.Password.Any(char.IsLower) || !request.Password.Any(char.IsDigit)))
            return "Password must be at least 8 characters and include uppercase, lowercase, and a number.";
        return null;
    }
    private static bool IsUsersUniqueViolation(DbUpdateException ex)
    {
        for (Exception? current = ex; current != null; current = current.InnerException)
            if (current.Message.Contains("1062", StringComparison.OrdinalIgnoreCase) &&
                (current.Message.Contains("users_username_unique", StringComparison.OrdinalIgnoreCase) ||
                 current.Message.Contains("users_email_unique", StringComparison.OrdinalIgnoreCase))) return true;
        return false;
    }
}

public abstract class UserRequest
{
    public string Name { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string? Password { get; set; }
    public string? Tel { get; set; }
    public string? Position { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class CreateUserRequest : UserRequest;
public sealed class UpdateUserRequest : UserRequest;
public sealed record UserResponse(uint Id, string Name, string Username, string Email, string Roles,
    string Tel, string Position, bool IsActive);
