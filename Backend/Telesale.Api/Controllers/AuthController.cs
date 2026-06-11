using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;
using Telesale.Api.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace Telesale.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly TelesaleDbContext _db;

    public AuthController(TelesaleDbContext db)
    {
        _db = db;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Username and password are required" });
        }

        var user = await _db.users
            .FirstOrDefaultAsync(u => u.username == request.Username, cancellationToken);

        if (user == null)
        {
            return Unauthorized(new { message = "Invalid username or password" });
        }

        // Brute-force account lockout check
        if (user.locked_until.HasValue && user.locked_until.Value > DateTime.UtcNow)
        {
            return StatusCode(StatusCodes.Status423Locked, new 
            { 
                message = "Account is temporarily locked due to multiple failed attempts. Please try again later." 
            });
        }

        // Verify password securely using BCrypt (no plain-text fallback)
        bool isValid = false;
        try
        {
            isValid = BCrypt.Net.BCrypt.Verify(request.Password, user.password);
        }
        catch
        {
            isValid = false;
        }

        if (!isValid)
        {
            user.failed_login_count++;
            if (user.failed_login_count >= 5)
            {
                user.locked_until = DateTime.UtcNow.AddMinutes(15);
            }
            await _db.SaveChangesAsync(cancellationToken);

            return Unauthorized(new { message = "Invalid username or password" });
        }

        // Reject inactive users
        if (user.is_active == false)
        {
            return Unauthorized(new { message = "Invalid username or password" });
        }

        // Successful login: Reset login failure metrics and record login time
        user.failed_login_count = 0;
        user.locked_until = null;
        user.last_login_at = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        // Sign in via Cookie Authentication with minimal claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.username),
            new Claim(ClaimTypes.NameIdentifier, user.id.ToString()),
            new Claim(ClaimTypes.Role, user.roles),
            new Claim("Position", user.position ?? "")
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        var initials = GetInitials(user.name);

        return Ok(new
        {
            id = user.id,
            username = user.username,
            name = user.name,
            email = user.email,
            roles = user.roles,
            avatar = initials
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Logged out successfully" });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        }

        var user = await _db.users
            .FirstOrDefaultAsync(u => u.username == username && (u.is_active == null || u.is_active == true), cancellationToken);

        if (user == null)
        {
            return Unauthorized();
        }

        var initials = GetInitials(user.name);

        return Ok(new
        {
            id = user.id,
            username = user.username,
            name = user.name,
            email = user.email,
            roles = user.roles,
            avatar = initials
        });
    }

    private static string GetInitials(string name)
    {
        var initials = "";
        if (!string.IsNullOrEmpty(name))
        {
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                initials = parts[0][0].ToString();
                if (parts.Length > 1)
                {
                    initials += parts[1][0].ToString();
                }
            }
        }
        return string.IsNullOrEmpty(initials) ? "U" : initials;
    }
}

public class LoginRequest
{
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
}
