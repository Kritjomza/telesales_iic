using Microsoft.AspNetCore.Authorization;
using Telesale.Api.Controllers;

namespace Telesale.Api.Tests;

public class UsersAuthorizationTests
{
    [Fact]
    public void UsersController_RequiresAuthentication_ForEveryEndpoint()
    {
        var authorize = typeof(UsersController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true);

        Assert.NotEmpty(authorize);
        Assert.DoesNotContain(
            typeof(UsersController).GetMethods(),
            method => method.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Length > 0);
    }
}
