using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Telesale.Api.Helpers;

namespace Telesale.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly TelesaleDbContext _db;

    public DashboardController(TelesaleDbContext db)
    {
        _db = db;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        if (!User.CanReadReports())
        {
            return Forbid();
        }

        IQueryable<Telesale.Api.Models.user> usersQuery = _db.users.Where(u => u.is_active == null || u.is_active == true);
        IQueryable<Telesale.Api.Models.customer> customersQuery = _db.customers.ApplyCustomerScope(User, _db);

        if (User.IsSupervisor())
        {
            var position = User.GetUserPosition();
            usersQuery = usersQuery.Where(u => u.position == position && !string.IsNullOrEmpty(position));
        }

        var usersCount = await usersQuery.CountAsync(cancellationToken);
        var customersCount = await customersQuery.CountAsync(cancellationToken);
        var productsCount = await _db.products.CountAsync(cancellationToken);
        var brandsCount = await _db.brands.CountAsync(cancellationToken);

        return Ok(new
        {
            users = usersCount,
            customers = customersCount,
            products = productsCount,
            brands = brandsCount,
            generatedAt = DateTimeOffset.UtcNow
        });
    }
}
