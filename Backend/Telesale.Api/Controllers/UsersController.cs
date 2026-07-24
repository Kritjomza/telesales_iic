using System.Net.Mail;
using System.Text.Json.Serialization;
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
    private static readonly HashSet<string> AllowedRoles =
    [
        AppRoles.Admin,
        AppRoles.SuperAdmin,
        AppRoles.Manager,
        AppRoles.TeleSale,
        AppRoles.Sale
    ];

    private readonly TelesaleDbContext _db;

    public UsersController(TelesaleDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        if (!User.IsAdmin()) return Forbid();

        IQueryable<user> query = _db.users.AsNoTracking();
        if (User.GetUserRole() == AppRoles.Admin)
            query = query.Where(u => u.roles != AppRoles.SuperAdmin);

        var list = await query
            .OrderBy(u => u.id)
            .Select(u => new UserResponse(
                u.id, u.name, u.username, u.email, u.roles,
                u.tel ?? "", u.position ?? "", u.is_active ?? true))
            .ToListAsync(cancellationToken);
        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.IsAdmin()) return Forbid();

        var role = AppRoles.Normalize(request.Role);
        if (User.GetUserRole() == AppRoles.Admin && role == AppRoles.SuperAdmin)
            return Forbid();

        var validationError = ValidateUserInput(
            request.Name, request.Username, request.Email, role,
            request.Password, passwordRequired: true);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        var name = request.Name.Trim();
        var username = request.Username.Trim();
        var email = request.Email.Trim();
        var duplicate = await FindDuplicateAsync(username, email, null, cancellationToken);
        if (duplicate != null)
            return Conflict(new { message = duplicate });

        var now = DateTime.UtcNow;
        var entity = new user
        {
            name = name,
            username = username,
            email = email,
            roles = role,
            password = BCrypt.Net.BCrypt.HashPassword(request.Password),
            tel = NormalizeOptional(request.Tel),
            position = NormalizeOptional(request.Position),
            is_active = request.IsActive,
            created_at = now,
            updated_at = now
        };
        _db.users.Add(entity);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUsersUniqueViolation(ex))
        {
            return Conflict(new { message = "Username or email already exists." });
        }

        return CreatedAtAction(nameof(GetUsers), new { id = entity.id }, ToResponse(entity));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(
        uint id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.IsAdmin()) return Forbid();

        var target = await _db.users.FindAsync([id], cancellationToken);
        if (target == null)
            return NotFound(new { message = "User not found." });

        var role = AppRoles.Normalize(request.Role);
        if (User.GetUserRole() == AppRoles.Admin &&
            (target.roles == AppRoles.SuperAdmin || role == AppRoles.SuperAdmin))
            return Forbid();

        var validationError = ValidateUserInput(
            request.Name, request.Username, request.Email, role,
            request.Password, passwordRequired: false);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        var name = request.Name.Trim();
        var username = request.Username.Trim();
        var email = request.Email.Trim();
        var duplicate = await FindDuplicateAsync(username, email, id, cancellationToken);
        if (duplicate != null)
            return Conflict(new { message = duplicate });

        target.name = name;
        target.username = username;
        target.email = email;
        target.roles = role;
        target.tel = NormalizeOptional(request.Tel);
        target.position = NormalizeOptional(request.Position);
        target.is_active = request.IsActive;
        target.updated_at = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Password))
            target.password = BCrypt.Net.BCrypt.HashPassword(request.Password);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUsersUniqueViolation(ex))
        {
            return Conflict(new { message = "Username or email already exists." });
        }
        return Ok(ToResponse(target));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(uint id, CancellationToken cancellationToken)
    {
        if (!User.IsAdmin()) return Forbid();

        var target = await _db.users.FindAsync([id], cancellationToken);
        if (target == null)
            return NotFound(new { message = "User not found." });
        if (User.GetUserRole() == AppRoles.Admin && target.roles == AppRoles.SuperAdmin)
            return Forbid();
        if (User.GetUserId() == id)
            return Conflict(new { message = "You cannot delete your own account." });
        if (target.roles == AppRoles.SuperAdmin &&
            await _db.users.CountAsync(u => u.roles == AppRoles.SuperAdmin, cancellationToken) <= 1)
            return Conflict(new { message = "The last Super Admin cannot be deleted." });
        if (await HasProtectedReferencesAsync(id, cancellationToken))
            return Conflict(new { message = "This user is referenced by business records and cannot be deleted." });

        _db.users.Remove(target);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "User deleted successfully." });
    }

    private async Task<string?> FindDuplicateAsync(
        string username,
        string email,
        uint? excludedId,
        CancellationToken cancellationToken)
    {
        if (await _db.users.AnyAsync(
                u => (!excludedId.HasValue || u.id != excludedId.Value) && u.username == username,
                cancellationToken))
            return "Username already exists.";
        if (await _db.users.AnyAsync(
                u => (!excludedId.HasValue || u.id != excludedId.Value) && u.email == email,
                cancellationToken))
            return "Email already exists.";
        return null;
    }

    private async Task<bool> HasProtectedReferencesAsync(uint id, CancellationToken cancellationToken)
    {
        if (id > int.MaxValue) return true;
        var signedId = (int)id;
        return await _db.customers.AnyAsync(
                   c => c.owner_id == signedId ||
                        c.updated_user == signedId ||
                        c.sale_id == signedId ||
                        c.telesale_id == signedId ||
                        c.sale_id_bak == signedId ||
                        c.telesale_id_bak == signedId,
                   cancellationToken) ||
               await _db.targets.AnyAsync(t => t.user_id == signedId, cancellationToken) ||
               await _db.import_sessions.AnyAsync(i => i.imported_by == id, cancellationToken) ||
               await _db.assignment_histories.AnyAsync(
                   h => h.changed_by_id == id ||
                        h.old_sale_id == signedId ||
                        h.new_sale_id == signedId ||
                        h.old_telesale_id == signedId ||
                        h.new_telesale_id == signedId,
                   cancellationToken);
    }

    private static string? ValidateUserInput(
        string? name,
        string? username,
        string? email,
        string role,
        string? password,
        bool passwordRequired)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 255)
            return "Name is required and must not exceed 255 characters.";
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length > 255)
            return "Username is required and must not exceed 255 characters.";
        if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 255 || !IsValidEmail(email.Trim()))
            return "A valid email address is required.";
        if (!AllowedRoles.Contains(role))
            return "Invalid role.";
        if (passwordRequired && string.IsNullOrWhiteSpace(password))
            return "Password is required.";
        if (!string.IsNullOrWhiteSpace(password) &&
            (password.Length < 8 ||
             password.Length > 72 ||
             !password.Any(char.IsLetter) ||
             !password.Any(char.IsDigit)))
            return "Password must be 8-72 characters and include a letter and a number.";
        return null;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            return new MailAddress(email).Address == email;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static UserResponse ToResponse(user entity) => new(
        entity.id,
        entity.name,
        entity.username,
        entity.email,
        entity.roles,
        entity.tel ?? "",
        entity.position ?? "",
        entity.is_active ?? true);

    private static bool IsUsersUniqueViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current.Message.Contains("1062", StringComparison.OrdinalIgnoreCase) &&
                (current.Message.Contains("users_username_unique", StringComparison.OrdinalIgnoreCase) ||
                 current.Message.Contains("users_email_unique", StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }
}

public sealed class CreateUserRequest
{
    public string Name { get; set; } = "";
    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Tel { get; set; }
    public string? Position { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateUserRequest
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

public sealed record UserResponse(
    uint Id,
    string Name,
    string Username,
    string Email,
    string Roles,
    string Tel,
    string Position,
    [property: JsonPropertyName("is_active")] bool IsActive);
