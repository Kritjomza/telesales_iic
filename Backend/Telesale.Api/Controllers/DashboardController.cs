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
        var role = User.GetUserRole();
        if (role == "Sale" || role == "Tele Sale" || role == "Tele sale")
        {
            return Forbid();
        }

        var userId = User.GetUserId();
        var position = User.GetUserPosition();

        IQueryable<Telesale.Api.Models.user> usersQuery = _db.users.Where(u => u.is_active == null || u.is_active == true);
        IQueryable<Telesale.Api.Models.customer> customersQuery = _db.customers;

        if (role == "Manager" || role == "Supervisor")
        {
            usersQuery = usersQuery.Where(u => u.position == position && !string.IsNullOrEmpty(position));
            customersQuery = customersQuery.Where(c =>
                (c.sale_id != null && _db.users.Any(u => u.id == c.sale_id && u.position == position && u.position != null && u.position != "")) ||
                (c.telesale_id != null && _db.users.Any(u => u.id == c.telesale_id && u.position == position && u.position != null && u.position != "")) ||
                c.owner_id == (int)userId!.Value
            );
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