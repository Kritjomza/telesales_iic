using System.Security.Claims;
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
    [Theory]
    [InlineData(AppRoles.Manager)]
    [InlineData(AppRoles.Sale)]
    [InlineData(AppRoles.TeleSale)]
    public async Task WriteEndpoints_RejectOtherRoles(string role)
    {
        await using var db = CreateDb();
        var controller = CreateController(db, 9, role);

        Assert.IsType<ForbidResult>(await controller.CreateUser(ValidCreate(), default));
        Assert.IsType<ForbidResult>(await controller.UpdateUser(20, ValidUpdate(), default));
        Assert.IsType<ForbidResult>(await controller.DeleteUser(20, default));
    }

    [Theory]
    [InlineData(AppRoles.Admin)]
    [InlineData(AppRoles.SuperAdmin)]
    public async Task CreateUser_AdminRoles_CreateHashedUser(string role)
    {
        await using var db = CreateDb();
        var result = await CreateController(db, 1, role).CreateUser(ValidCreate(), default);

        Assert.IsType<CreatedAtActionResult>(result);
        var saved = Assert.Single(db.users);
        Assert.True(BCrypt.Net.BCrypt.Verify("SecurePass1!", saved.password));
        Assert.DoesNotContain("password", result.ToString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_CannotCreateOrManageSuperAdmin()
    {
        await using var db = CreateDb();
        db.users.Add(User(20, AppRoles.SuperAdmin));
        await db.SaveChangesAsync();
        var controller = CreateController(db, 1, AppRoles.Admin);
        var create = ValidCreate();
        create.Role = AppRoles.SuperAdmin;
        var update = ValidUpdate();
        update.Role = AppRoles.Admin;

        Assert.IsType<ForbidResult>(await controller.CreateUser(create, default));
        Assert.IsType<ForbidResult>(await controller.UpdateUser(20, update, default));
        Assert.IsType<ForbidResult>(await controller.DeleteUser(20, default));
    }

    [Fact]
    public async Task UpdateUser_BlankPassword_PreservesHash()
    {
        await using var db = CreateDb();
        var existing = User(20, AppRoles.Sale);
        var originalHash = existing.password;
        db.users.Add(existing);
        await db.SaveChangesAsync();
        var request = ValidUpdate();
        request.Password = " ";

        Assert.IsType<OkObjectResult>(await CreateController(db, 1, AppRoles.SuperAdmin).UpdateUser(20, request, default));
        Assert.Equal(originalHash, existing.password);
    }

    [Fact]
    public async Task DuplicateUsernameOrEmail_ReturnsConflict()
    {
        await using var db = CreateDb();
        db.users.Add(User(20, AppRoles.Sale));
        await db.SaveChangesAsync();
        var request = ValidCreate();
        request.Username = "user20";

        Assert.IsType<ConflictObjectResult>(await CreateController(db, 1, AppRoles.SuperAdmin).CreateUser(request, default));
    }

    [Fact]
    public async Task DeleteUser_RejectsSelfAndReferencedUser()
    {
        await using var db = CreateDb();
        db.users.AddRange(User(1, AppRoles.Admin), User(20, AppRoles.Sale));
        db.customers.Add(new customer { id = 1, name = "Protected", sale_id = 20, status = "New", create_type = "Key", is_active = true });
        await db.SaveChangesAsync();
        var controller = CreateController(db, 1, AppRoles.Admin);

        Assert.IsType<ConflictObjectResult>(await controller.DeleteUser(1, default));
        Assert.IsType<ConflictObjectResult>(await controller.DeleteUser(20, default));
        Assert.Equal(2, db.users.Count());
    }

    [Fact]
    public async Task DeleteUser_UnreferencedUser_IsDeleted()
    {
        await using var db = CreateDb();
        db.users.Add(User(20, AppRoles.Sale));
        await db.SaveChangesAsync();

        Assert.IsType<NoContentResult>(await CreateController(db, 1, AppRoles.Admin).DeleteUser(20, default));
        Assert.Empty(db.users);
    }

    private static TelesaleDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<TelesaleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static UsersController CreateController(TelesaleDbContext db, uint id, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Name, $"user{id}"),
            new Claim(ClaimTypes.Role, role)
        };
        var controller = new UsersController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };
        return controller;
    }

    private static CreateUserRequest ValidCreate() => new()
    {
        Name = "New User", Username = "newuser", Email = "new@example.com",
        Role = AppRoles.Sale, Password = "SecurePass1!", IsActive = true
    };

    private static UpdateUserRequest ValidUpdate() => new()
    {
        Name = "Updated User", Username = "updated", Email = "updated@example.com",
        Role = AppRoles.Sale, IsActive = true
    };

    private static user User(uint id, string role) => new()
    {
        id = id, name = $"User {id}", username = $"user{id}", email = $"user{id}@example.com",
        roles = role, password = BCrypt.Net.BCrypt.HashPassword("ExistingPass1!"), is_active = true
    };
}
