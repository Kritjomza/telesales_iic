using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Telesale.Api.Helpers;

namespace Telesale.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly TelesaleDbContext _db;

    public UsersController(TelesaleDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        if (!User.CanManageAssignments())
        {
            return Forbid();
        }

        var position = User.GetUserPosition();
        IQueryable<Telesale.Api.Models.user> query = _db.users.AsNoTracking()
            .Where(u => u.is_active == null || u.is_active == true);

        if (User.IsSupervisor())
        {
            query = query.Where(u => u.position == position && !string.IsNullOrEmpty(position));
        }

        var list = await query
            .Select(u => new
            {
                id = u.id,
                username = u.username,
                name = u.name,
                email = u.email,
                roles = u.roles,
                tel = u.tel ?? "",
                position = u.position ?? "",
                linetoken = u.linetoken ?? ""
            })
            .ToListAsync(cancellationToken);

        return Ok(list);
    }
}
