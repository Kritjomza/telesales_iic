using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Telesale.Api.Controllers;
using Telesale.Api.Data;
using Telesale.Api.Helpers;
using Telesale.Api.Models;
using Telesale.Api.Services;
using Xunit;

namespace Telesale.Api.Tests;

public class PermissionTests
{
    private class DummyEmailService : IEmailNotificationService
    {
        public Task NotifyAdminEditAsync(string adminUsername, string actionName, string entityType, string entityId, string details)
        {
            return Task.CompletedTask;
        }
    }

    private class MockEmailService : IEmailNotificationService
    {
        public string AdminUsername { get; set; } = "";
        public string ActionName { get; set; } = "";
        public string EntityType { get; set; } = "";
        public string EntityId { get; set; } = "";
        public int CallCount { get; set; }

        public Task NotifyAdminEditAsync(string adminUsername, string actionName, string entityType, string entityId, string details)
        {
            AdminUsername = adminUsername;
            ActionName = actionName;
            EntityType = entityType;
            EntityId = entityId;
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private TelesaleDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<TelesaleDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new TelesaleDbContext(options);
    }

    private ClaimsPrincipal CreateUserPrincipal(uint id, string role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim(ClaimTypes.Name, "TestUser_" + role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task TestCustomerAccessUnlimited_Sale()
    {
        using var db = GetInMemoryDbContext();
        var saleUser = CreateUserPrincipal(10, AppRoles.Sale);

        var owned = new customer { id = 1, name = "Owned", owner_id = 10, status = "New", create_type = "Key", is_active = true };
        var assigned = new customer { id = 2, name = "Assigned", sale_id = 10, status = "New", create_type = "Key", is_active = true };
        var unrelated = new customer { id = 3, name = "Unrelated", sale_id = 99, owner_id = 99, status = "New", create_type = "Key", is_active = true };

        db.customers.AddRange(owned, assigned, unrelated);
        await db.SaveChangesAsync();

        Assert.True(await saleUser.HasCustomerAccessAsync(owned, db));
        Assert.True(await saleUser.HasCustomerAccessAsync(assigned, db));
        Assert.True(await saleUser.HasCustomerAccessAsync(unrelated, db));

        var scopedList = await db.customers.ApplyCustomerScope(saleUser, db).ToListAsync();
        Assert.Equal(3, scopedList.Count);
        Assert.Contains(scopedList, c => c.name == "Assigned");
        Assert.Contains(scopedList, c => c.name == "Owned");
        Assert.Contains(scopedList, c => c.name == "Unrelated");
    }

    [Fact]
    public async Task TestCustomerAccessUnlimited_TeleSale()
    {
        using var db = GetInMemoryDbContext();
        var teleUser = CreateUserPrincipal(20, AppRoles.TeleSale);

        var owned = new customer { id = 1, name = "Owned", owner_id = 20, status = "New", create_type = "Key", is_active = true };
        var assigned = new customer { id = 2, name = "Assigned", telesale_id = 20, status = "New", create_type = "Key", is_active = true };
        var unrelated = new customer { id = 3, name = "Unrelated", telesale_id = 99, owner_id = 99, status = "New", create_type = "Key", is_active = true };

        db.customers.AddRange(owned, assigned, unrelated);
        await db.SaveChangesAsync();

        Assert.True(await teleUser.HasCustomerAccessAsync(owned, db));
        Assert.True(await teleUser.HasCustomerAccessAsync(assigned, db));
        Assert.True(await teleUser.HasCustomerAccessAsync(unrelated, db));

        var scopedList = await db.customers.ApplyCustomerScope(teleUser, db).ToListAsync();
        Assert.Equal(3, scopedList.Count);
        Assert.Contains(scopedList, c => c.name == "Assigned");
        Assert.Contains(scopedList, c => c.name == "Owned");
        Assert.Contains(scopedList, c => c.name == "Unrelated");
    }

    [Fact]
    public async Task TestAdminAccessPreserved()
    {
        using var db = GetInMemoryDbContext();
        var adminUser = CreateUserPrincipal(5, AppRoles.Admin);
        var superUser = CreateUserPrincipal(6, AppRoles.SuperAdmin);

        var customer = new customer { id = 1, name = "Any", owner_id = 99, status = "New", create_type = "Key", is_active = true };
        db.customers.Add(customer);
        await db.SaveChangesAsync();

        Assert.True(await adminUser.HasCustomerAccessAsync(customer, db));
        Assert.True(await superUser.HasCustomerAccessAsync(customer, db));
    }

    [Fact]
    public async Task TestSaleDeleteCustomerDenied_Returns403()
    {
        using var db = GetInMemoryDbContext();
        var saleUser = CreateUserPrincipal(10, AppRoles.Sale);
        var c = new customer { id = 1, name = "Test", owner_id = 10, status = "New", create_type = "Key", is_active = true };
        db.customers.Add(c);
        await db.SaveChangesAsync();

        var controller = new CustomersController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = saleUser }
        };

        var result = await controller.DeleteCustomer(1, default);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);

        var val = objectResult.Value;
        Assert.NotNull(val);
        var prop = val.GetType().GetProperty("message");
        Assert.NotNull(prop);
        var msg = prop.GetValue(val) as string;
        Assert.Equal("You do not have permission to delete customer records.", msg);
    }

    [Fact]
    public async Task TestAdminDeleteCustomerDenied_Returns403()
    {
        using var db = GetInMemoryDbContext();
        var adminUser = CreateUserPrincipal(5, AppRoles.Admin);
        var c = new customer { id = 1, name = "Test", owner_id = 10, status = "New", create_type = "Key", is_active = true };
        db.customers.Add(c);
        await db.SaveChangesAsync();

        var controller = new CustomersController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = adminUser }
        };

        var result = await controller.DeleteCustomer(1, default);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task TestSuperAdminDeleteCustomerAllowed_ReturnsOk()
    {
        using var db = GetInMemoryDbContext();
        var superUser = CreateUserPrincipal(6, AppRoles.SuperAdmin);
        var c = new customer { id = 1, name = "Test", owner_id = 10, status = "New", create_type = "Key", is_active = true };
        db.customers.Add(c);
        await db.SaveChangesAsync();

        var controller = new CustomersController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = superUser }
        };

        var result = await controller.DeleteCustomer(1, default);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task UpdateCustomerStatus_AllowsOnlyCallStatusAndUpdatesNoOtherField()
    {
        using var db = GetInMemoryDbContext();
        var superUser = CreateUserPrincipal(6, AppRoles.SuperAdmin);
        var c = new customer
        {
            id = 1,
            name = "Test",
            address = "Original Address",
            owner_id = 10,
            status = "Not Called",
            create_type = "Key",
            is_active = true
        };
        db.customers.Add(c);
        await db.SaveChangesAsync();

        var controller = new CustomersController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = superUser }
            }
        };

        var result = await controller.UpdateCustomerStatus(1, new CustomerStatusUpdateDto { status = "Called" }, default);

        Assert.IsType<OkObjectResult>(result);
        var saved = await db.customers.FindAsync((uint)1);
        Assert.NotNull(saved);
        Assert.Equal("Called", saved!.status);
        Assert.Equal("Original Address", saved.address);
    }

    [Fact]
    public async Task UpdateCustomerStatus_RejectsInvalidStatus()
    {
        using var db = GetInMemoryDbContext();
        var superUser = CreateUserPrincipal(6, AppRoles.SuperAdmin);
        var c = new customer { id = 1, name = "Test", owner_id = 10, status = "Not Called", create_type = "Key", is_active = true };
        db.customers.Add(c);
        await db.SaveChangesAsync();

        var controller = new CustomersController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = superUser }
            }
        };

        var result = await controller.UpdateCustomerStatus(1, new CustomerStatusUpdateDto { status = "Win" }, default);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Invalid customer status", badRequest.Value?.ToString());
        var saved = await db.customers.FindAsync((uint)1);
        Assert.Equal("Not Called", saved!.status);
    }

    [Fact]
    public async Task GetCustomers_RejectsInvalidStatusFilter()
    {
        using var db = GetInMemoryDbContext();
        var superUser = CreateUserPrincipal(6, AppRoles.SuperAdmin);
        db.customers.Add(new customer { id = 1, name = "Test", owner_id = 10, status = "Not Called", create_type = "Key", is_active = true });
        await db.SaveChangesAsync();

        var controller = new CustomersController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = superUser }
            }
        };

        var result = await controller.GetCustomers(
            page: 1,
            pageSize: 25,
            search: null,
            completeness: null,
            missingField: null,
            cancellationToken: default,
            businessType: null,
            saleId: null,
            telesaleId: null,
            status: "Win");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("Invalid customer status", badRequest.Value?.ToString());
    }

    [Fact]
    public async Task TestSuperAdminDeleteCustomerWithChildren_CascadeDeletes()
    {
        using var db = GetInMemoryDbContext();
        var superUser = CreateUserPrincipal(6, AppRoles.SuperAdmin);
        
        var c = new customer { id = 1, name = "CustomerToCascade", owner_id = 10, status = "New", create_type = "Key", is_active = true };
        db.customers.Add(c);

        var d = new detail { id = 100, cust_id = 1, contact_name = "Contact 1", bak_point = 0, point = 0, total_point = 0, is_active = true };
        db.details.Add(d);

        var dev = new detail_device { id = 200, dtl_id = 100, progress_status = "New" };
        db.detail_devices.Add(dev);

        var pj = new detail_pj { id = 300, dtl_id = 100, progress_status = "Discuss" };
        db.detail_pjs.Add(pj);

        await db.SaveChangesAsync();

        var controller = new CustomersController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = superUser }
        };

        var result = await controller.DeleteCustomer(1, default);
        Assert.IsType<OkObjectResult>(result);

        // Verify all related records are removed from DB sets
        Assert.Empty(await db.customers.ToListAsync());
        Assert.Empty(await db.details.ToListAsync());
        Assert.Empty(await db.detail_devices.ToListAsync());
        Assert.Empty(await db.detail_pjs.ToListAsync());
    }

    [Fact]
    public async Task TestNotifyAdminEditFilter_TriggersForAdmin()
    {
        var emailService = new MockEmailService();
        var services = new ServiceCollection();
        services.AddSingleton<IEmailNotificationService>(emailService);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = serviceProvider;
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = "/api/customers/123";
        httpContext.User = CreateUserPrincipal(1, AppRoles.Admin);
        httpContext.Response.StatusCode = 200;

        var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var executingContext = new ResultExecutingContext(actionContext, new List<IFilterMetadata>(), new OkResult(), new object());

        var filter = new NotifyAdminEditAttribute();
        bool nextCalled = false;
        ResultExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ResultExecutedContext(actionContext, new List<IFilterMetadata>(), new OkResult(), new object()));
        };

        await filter.OnResultExecutionAsync(executingContext, next);

        Assert.True(nextCalled);
        Assert.Equal(1, emailService.CallCount);
        Assert.Equal("customers", emailService.EntityType);
        Assert.Equal("123", emailService.EntityId);
    }

    [Fact]
    public void TestAdminWriteOnlyFilter_SuperAdminAllowed()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = "/api/masterdata/categories";
        httpContext.User = CreateUserPrincipal(1, AppRoles.SuperAdmin);

        var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var context = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object>(), new object());

        var filter = new AdminWriteOnlyAttribute();
        filter.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void TestAdminWriteOnlyFilter_ManagerRestrictedCategory()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = "/api/masterdata/categories";
        httpContext.User = CreateUserPrincipal(1, AppRoles.Manager);

        var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var context = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object>(), new object());

        var filter = new AdminWriteOnlyAttribute();
        filter.OnActionExecuting(context);

        Assert.IsType<ForbidResult>(context.Result);
    }

    [Fact]
    public void TestAdminWriteOnlyFilter_ManagerAllowedBrand()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = "/api/masterdata/brands";
        httpContext.User = CreateUserPrincipal(1, AppRoles.Manager);

        var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var context = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object>(), new object());

        var filter = new AdminWriteOnlyAttribute();
        filter.OnActionExecuting(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void TestAdminWriteOnlyFilter_AdminDeleteDenied()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "DELETE";
        httpContext.Request.Path = "/api/masterdata/brands/1";
        httpContext.User = CreateUserPrincipal(1, AppRoles.Admin);

        var actionContext = new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var context = new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object>(), new object());

        var filter = new AdminWriteOnlyAttribute();
        filter.OnActionExecuting(context);

        Assert.IsType<ForbidResult>(context.Result);
    }

    [Fact]
    public async Task DumpDbUsers()
    {
        var options = new DbContextOptionsBuilder<TelesaleDbContext>()
            .UseMySql("Server=127.0.0.1;Port=3306;Database=sale;User=sale;Password=1234;", ServerVersion.AutoDetect("Server=127.0.0.1;Port=3306;Database=sale;User=sale;Password=1234;"))
            .Options;
        using var db = new TelesaleDbContext(options);
        var usersList = await db.users.ToListAsync();
        var message = string.Join("\n", usersList.Select(u => $"ID: {u.id}, User: {u.username}, Role: {u.roles}, Active: {u.is_active}"));
        throw new Exception("DATABASE_USERS:\n" + message);
    }
}
