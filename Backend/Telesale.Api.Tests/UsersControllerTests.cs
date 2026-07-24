using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Controllers;
using Telesale.Api.Data;
using Telesale.Api.Helpers;
using Telesale.Api.Models;

namespace Telesale.Api.Tests;

public class UsersControllerTests
{
    [Fact]
    public void Controller_RequiresAuthentication()
    {
        Assert.NotNull(typeof(UsersController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Theory]
    [InlineData(AppRoles.Manager)]
    [InlineData(AppRoles.Sale)]
    [InlineData(AppRoles.TeleSale)]
    public async Task CreateUser_OtherAuthenticatedRole_ReturnsForbid(string role)
    {
        await using var db = CreateDb();
        var result = await CreateController(db, 10, role).CreateUser(ValidCreateRequest(), default);
        Assert.IsType<ForbidResult>(result);
    }

    [Theory]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.SuperAdmin)]
    public async Task CreateUser_AdminRoles_CreatesSafeResponseWithHashedPassword(string role)
    {
        await using var db = CreateDb();
        var result = await CreateController(db, 1, role).CreateUser(ValidCreateRequest(), default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var json = JsonSerializer.Serialize(created.Value).ToLowerInvariant();
        Assert.DoesNotContain("password", json);
        Assert.DoesNotContain("linetoken", json);
        Assert.DoesNotContain("remember_token", json);
        Assert.DoesNotContain("locked_until", json);
        Assert.Contains("is_active", json);

        var entity = Assert.Single(db.users);
        Assert.NotEqual("ValidPass1!", entity.password);
        Assert.True(BCrypt.Net.BCrypt.Verify("ValidPass1!", entity.password));
    }

    [Fact]
    public async Task GetUsers_AdminSeesOnlyOrdinaryUsers()
    {
        await using var db = CreateDb();
        db.users.AddRange(UserEntity(2, AppRoles.SuperAdmin), UserEntity(3, AppRoles.Sale));
        await db.SaveChangesAsync();

        var result = await CreateController(db, 1, AppRoles.Admin).GetUsers(default);
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.DoesNotContain("Super Admin", json);
        Assert.Contains("Sale", json);
    }

    [Fact]
    public async Task GetUsers_SuperAdminSeesAllUsers()
    {
        await using var db = CreateDb();
        db.users.AddRange(UserEntity(2, AppRoles.SuperAdmin), UserEntity(3, AppRoles.Sale));
        await db.SaveChangesAsync();

        var result = await CreateController(db, 1, AppRoles.SuperAdmin).GetUsers(default);
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value);
        Assert.Contains("Super Admin", json);
        Assert.Contains("Sale", json);
    }

    [Fact]
    public async Task GetUsers_OtherAuthenticatedRole_ReturnsForbid()
    {
        await using var db = CreateDb();
        var result = await CreateController(db, 1, AppRoles.Sale).GetUsers(default);
        Assert.IsType<ForbidResult>(result);
    }
    [Fact]
    public async Task GetUsers_ReturnsOnlySafeFields()
    {
        await using var db = CreateDb();
        db.users.Add(UserEntity(2, AppRoles.Sale));
        await db.SaveChangesAsync();

        var result = await CreateController(db, 1, AppRoles.Admin).GetUsers(default);
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value).ToLowerInvariant();
        Assert.DoesNotContain("password", json);
        Assert.DoesNotContain("linetoken", json);
        Assert.DoesNotContain("remember_token", json);
        Assert.DoesNotContain("failed_login_count", json);
        Assert.DoesNotContain("locked_until", json);
        Assert.Contains("is_active", json);
    }

    [Theory]
    [InlineData("", "user2@example.com", "Sale")]
    [InlineData("user2", "not-an-email", "Sale")]
    [InlineData("user2", "user2@example.com", "Viewer")]
    public async Task CreateUser_InvalidInput_ReturnsBadRequest(string username, string email, string role)
    {
        await using var db = CreateDb();
        var request = ValidCreateRequest();
        request.Username = username;
        request.Email = email;
        request.Role = role;

        var result = await CreateController(db, 1, AppRoles.SuperAdmin).CreateUser(request, default);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData("existing", "new@example.com")]
    [InlineData("new-user", "existing@example.com")]
    public async Task CreateUser_DuplicateUsernameOrEmail_ReturnsConflict(string username, string email)
    {
        await using var db = CreateDb();
        db.users.Add(UserEntity(2, AppRoles.Sale, username: "existing", email: "existing@example.com"));
        await db.SaveChangesAsync();
        var request = ValidCreateRequest();
        request.Username = username;
        request.Email = email;

        var result = await CreateController(db, 1, AppRoles.SuperAdmin).CreateUser(request, default);
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Admin_CannotCreateSuperAdmin()
    {
        await using var db = CreateDb();
        var request = ValidCreateRequest();
        request.Role = AppRoles.SuperAdmin;
        var result = await CreateController(db, 1, AppRoles.Admin).CreateUser(request, default);
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Admin_CannotEditExistingSuperAdmin()
    {
        await using var db = CreateDb();
        db.users.Add(UserEntity(2, AppRoles.SuperAdmin));
        await db.SaveChangesAsync();
        var result = await CreateController(db, 1, AppRoles.Admin).UpdateUser(2, ValidUpdateRequest(), default);
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Admin_CannotPromoteUserToSuperAdmin()
    {
        await using var db = CreateDb();
        db.users.Add(UserEntity(2, AppRoles.Sale));
        await db.SaveChangesAsync();
        var request = ValidUpdateRequest();
        request.Role = AppRoles.SuperAdmin;
        var result = await CreateController(db, 1, AppRoles.Admin).UpdateUser(2, request, default);
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdateUser_WithoutPassword_PreservesExistingHash()
    {
        await using var db = CreateDb();
        var existing = UserEntity(2, AppRoles.Sale);
        var originalHash = existing.password;
        db.users.Add(existing);
        await db.SaveChangesAsync();
        var request = ValidUpdateRequest();
        request.Password = " ";

        var result = await CreateController(db, 1, AppRoles.SuperAdmin).UpdateUser(2, request, default);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(originalHash, existing.password);
    }

    [Fact]
    public async Task UpdateUser_WithPassword_ReplacesHash()
    {
        await using var db = CreateDb();
        var existing = UserEntity(2, AppRoles.Sale);
        var originalHash = existing.password;
        db.users.Add(existing);
        await db.SaveChangesAsync();
        var request = ValidUpdateRequest();
        request.Password = "Replacement1!";

        var result = await CreateController(db, 1, AppRoles.SuperAdmin).UpdateUser(2, request, default);

        Assert.IsType<OkObjectResult>(result);
        Assert.NotEqual(originalHash, existing.password);
        Assert.True(BCrypt.Net.BCrypt.Verify("Replacement1!", existing.password));
    }

    [Fact]
    public async Task DeleteUser_BlocksSelfDeletion()
    {
        await using var db = CreateDb();
        db.users.Add(UserEntity(1, AppRoles.Admin));
        await db.SaveChangesAsync();
        var result = await CreateController(db, 1, AppRoles.Admin).DeleteUser(1, default);
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task DeleteUser_BlocksLastSuperAdmin()
    {
        await using var db = CreateDb();
        db.users.Add(UserEntity(1, AppRoles.SuperAdmin));
        await db.SaveChangesAsync();
        var result = await CreateController(db, 99, AppRoles.SuperAdmin).DeleteUser(1, default);
        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Admin_CannotDeleteSuperAdmin()
    {
        await using var db = CreateDb();
        db.users.AddRange(UserEntity(1, AppRoles.Admin), UserEntity(2, AppRoles.SuperAdmin));
        await db.SaveChangesAsync();
        var result = await CreateController(db, 1, AppRoles.Admin).DeleteUser(2, default);
        Assert.IsType<ForbidResult>(result);
    }

    [Theory]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.SuperAdmin)]
    public async Task DeleteUser_AdminRoles_DeleteUnreferencedOrdinaryUser(string role)
    {
        await using var db = CreateDb();
        db.users.AddRange(UserEntity(1, role), UserEntity(2, AppRoles.Sale));
        await db.SaveChangesAsync();
        var result = await CreateController(db, 1, role).DeleteUser(2, default);
        Assert.IsType<OkObjectResult>(result);
        Assert.Null(await db.users.FindAsync((uint)2));
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("target")]
    [InlineData("import")]
    [InlineData("assignment")]
    public async Task DeleteUser_ReferencedByProtectedRecord_ReturnsConflict(string referenceType)
    {
        await using var db = CreateDb();
        db.users.AddRange(UserEntity(1, AppRoles.SuperAdmin), UserEntity(2, AppRoles.Sale));
        if (referenceType == "customer")
            db.customers.Add(new customer { id = 1, name = "Protected", status = "New", create_type = "Key", sale_id = 2, is_active = true });
        if (referenceType == "target")
            db.targets.Add(new target { id = 1, user_id = 2 });
        if (referenceType == "import")
            db.import_sessions.Add(new import_session { id = 1, imported_by = 2 });
        if (referenceType == "assignment")
        {
            db.customers.Add(new customer { id = 1, name = "Protected", status = "New", create_type = "Key", is_active = true });
            db.assignment_histories.Add(new assignment_history { id = 1, customer_id = 1, changed_by_id = 2 });
        }
        await db.SaveChangesAsync();

        var result = await CreateController(db, 1, AppRoles.SuperAdmin).DeleteUser(2, default);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.NotNull(await db.users.FindAsync((uint)2));
    }

    private static TelesaleDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<TelesaleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TelesaleDbContext(options);
    }

    private static UsersController CreateController(TelesaleDbContext db, uint id, string role)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim(ClaimTypes.Name, $"user-{id}")
        ], "Test"));
        return new UsersController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }

    private static CreateUserRequest ValidCreateRequest() => new()
    {
        Name = "New User",
        Username = "new-user",
        Email = "new@example.com",
        Role = AppRoles.Sale,
        Password = "ValidPass1!",
        Tel = "0123456789",
        Position = "Sales",
        IsActive = true
    };

    private static UpdateUserRequest ValidUpdateRequest() => new()
    {
        Name = "Updated User",
        Username = "updated-user",
        Email = "updated@example.com",
        Role = AppRoles.Sale,
        Tel = "0123456789",
        Position = "Sales",
        IsActive = true
    };

    private static user UserEntity(
        uint id,
        string role,
        string? username = null,
        string? email = null) => new()
    {
        id = id,
        name = $"User {id}",
        username = username ?? $"user-{id}",
        email = email ?? $"user-{id}@example.com",
        roles = role,
        password = BCrypt.Net.BCrypt.HashPassword("ExistingPass1!"),
        is_active = true,
        linetoken = "secret-line-token",
        remember_token = "secret-remember-token"
    };
}
