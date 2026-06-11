using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;

namespace Telesale.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly TelesaleDbContext _db;

    public HealthController(TelesaleDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "ok",
            service = "Telesale.Api",
            time = DateTimeOffset.UtcNow
        });
    }

    [HttpGet("db")]
    public async Task<IActionResult> GetDatabaseHealth(CancellationToken cancellationToken)
    {
        var canConnect = await _db.Database.CanConnectAsync(cancellationToken);

        return Ok(new
        {
            database = "sale",
            canConnect,
            time = DateTimeOffset.UtcNow
        });
    }
}