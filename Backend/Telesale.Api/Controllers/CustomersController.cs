using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;
using Telesale.Api.Models;
using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Authorization;
using Telesale.Api.Helpers;

namespace Telesale.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly TelesaleDbContext _db;

    public CustomersController(TelesaleDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetCustomers(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        if (!User.CanReadManagementData()) return Forbid();

        IQueryable<customer> query = _db.customers.ApplyCustomerScope(User, _db);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(c => (c.name != null && c.name.ToLower().Contains(searchLower)) || (c.address != null && c.address.ToLower().Contains(searchLower)));
        }

        if (page.HasValue)
        {
            var size = pageSize ?? 10;
            if (size <= 0) return BadRequest("Page size must be greater than zero.");
            if (size > 100) return BadRequest("Page size cannot exceed 100.");
            if (page.Value <= 0) return BadRequest("Page number must be greater than zero.");

            var totalCount = await query.CountAsync(cancellationToken);
            var totalPages = (int)Math.Ceiling((double)totalCount / size);

            var rawItems = await query.AsNoTracking()
                .OrderBy(c => c.id)
                .Skip((page.Value - 1) * size)
                .Take(size)
                .Select(c => new
                {
                    c.id,
                    name = c.name ?? "Unnamed",
                    address = c.address ?? "",
                    c.capital,
                    c.telesale_id,
                    telesale = _db.users.Where(u => u.id == c.telesale_id).Select(u => u.name).FirstOrDefault(),
                    c.sale_id,
                    sale = _db.users.Where(u => u.id == c.sale_id).Select(u => u.name).FirstOrDefault(),
                    c.status,
                    is_active = c.is_active ?? true,
                    c.start_dt,
                    bt_type = _db.business_types.Where(bt => bt.id == c.business_type_id).Select(bt => bt.type).FirstOrDefault() ?? "Others",
                    hasCostSheet = _db.cost_sheets.Any(cs => cs.company == c.name),
                    updatedAt = c.updated_at ?? c.created_at
                })
                .ToListAsync(cancellationToken);

            var list = rawItems.Select(c => new CustomerResponseDto
            {
                id = c.id,
                name = c.name,
                address = c.address,
                capital = c.capital?.ToString() ?? "0",
                telesale_id = c.telesale_id,
                telesale = c.telesale,
                sale_id = c.sale_id,
                sale = c.sale,
                status = c.status,
                is_active = c.is_active,
                start_dt = c.start_dt?.ToString("yyyy-MM-dd"),
                bt_type = c.bt_type,
                renewalDays = c.start_dt.HasValue ? c.start_dt.Value.AddYears(1).DayNumber - today.DayNumber : 30,
                hasCostSheet = c.hasCostSheet,
                updatedAt = c.updatedAt?.ToString("yyyy-MM-dd") ?? ""
            }).ToList();

            return Ok(new
            {
                items = list,
                totalCount,
                page = page.Value,
                pageSize = size,
                totalPages
            });
        }
        else
        {
            var rawItems = await query.AsNoTracking()
                .OrderBy(c => c.id)
                .Take(500)
                .Select(c => new
                {
                    c.id,
                    name = c.name ?? "Unnamed",
                    address = c.address ?? "",
                    c.capital,
                    c.telesale_id,
                    telesale = _db.users.Where(u => u.id == c.telesale_id).Select(u => u.name).FirstOrDefault(),
                    c.sale_id,
                    sale = _db.users.Where(u => u.id == c.sale_id).Select(u => u.name).FirstOrDefault(),
                    c.status,
                    is_active = c.is_active ?? true,
                    c.start_dt,
                    bt_type = _db.business_types.Where(bt => bt.id == c.business_type_id).Select(bt => bt.type).FirstOrDefault() ?? "Others",
                    hasCostSheet = _db.cost_sheets.Any(cs => cs.company == c.name),
                    updatedAt = c.updated_at ?? c.created_at
                })
                .ToListAsync(cancellationToken);

            var list = rawItems.Select(c => new CustomerResponseDto
            {
                id = c.id,
                name = c.name,
                address = c.address,
                capital = c.capital?.ToString() ?? "0",
                telesale_id = c.telesale_id,
                telesale = c.telesale,
                sale_id = c.sale_id,
                sale = c.sale,
                status = c.status,
                is_active = c.is_active,
                start_dt = c.start_dt?.ToString("yyyy-MM-dd"),
                bt_type = c.bt_type,
                renewalDays = c.start_dt.HasValue ? c.start_dt.Value.AddYears(1).DayNumber - today.DayNumber : 30,
                hasCostSheet = c.hasCostSheet,
                updatedAt = c.updatedAt?.ToString("yyyy-MM-dd") ?? ""
            }).ToList();

            return Ok(list);
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateCustomer([FromBody] CustomerCreateDto dto, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var role = User.GetUserRole();
        if (!User.CanWriteCustomerWorkflow())
        {
            return Forbid();
        }

        var bt = await _db.business_types.FirstOrDefaultAsync(b => b.type == dto.bt_type, cancellationToken);
        int? btId = bt != null ? (int?)bt.id : null;

        var cleanCapital = dto.capital?.Replace(",", "") ?? "0";
        var c = new customer
        {
            name = dto.name,
            address = dto.address,
            phone = dto.phone,
            capital = double.TryParse(cleanCapital, out var cap) ? cap : 0,
            business_type_id = btId,
            status = "New",
            create_type = "Key",
            is_active = true,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow,
            start_dt = DateOnly.FromDateTime(DateTime.Today),
            owner_id = (int?)userId.Value
        };

        if (role == AppRoles.Sale)
        {
            c.sale_id = (int?)userId.Value;
            c.is_assign_sale = true;
            c.status = "Assigned";
        }
        else if (role == AppRoles.TeleSale)
        {
            c.telesale_id = (int?)userId.Value;
            c.is_assign_telesale = true;
            c.status = "Assigned";
        }

        _db.customers.Add(c);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await MapToResponseDto(c, cancellationToken));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCustomer(uint id, [FromBody] CustomerUpdateDto dto, CancellationToken cancellationToken)
    {
        var role = User.GetUserRole();
        if (!User.CanWriteCustomerWorkflow()) return Forbid();

        var c = await _db.customers.FindAsync(new object[] { id }, cancellationToken);
        if (c == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        if (!await HasCustomerAccess(c, cancellationToken))
        {
            return Forbid();
        }

        if (dto.name != null) c.name = dto.name;
        if (dto.address != null) c.address = dto.address;
        if (dto.phone != null) c.phone = dto.phone;
        if (dto.capital != null)
        {
            var cleanCapital = dto.capital.Replace(",", "");
            c.capital = double.TryParse(cleanCapital, out var cap) ? cap : c.capital;
        }
        if (dto.is_active.HasValue) c.is_active = dto.is_active.Value;
        if (dto.status != null) c.status = dto.status;
        
        if (dto.bt_type != null)
        {
            var bt = await _db.business_types.FirstOrDefaultAsync(b => b.type == dto.bt_type, cancellationToken);
            if (bt != null) c.business_type_id = (int)bt.id;
        }

        c.updated_at = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await MapToResponseDto(c, cancellationToken));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCustomer(uint id, CancellationToken cancellationToken)
    {
        var role = User.GetUserRole();
        if (!User.CanManageAssignments())
        {
            return Forbid();
        }

        var c = await _db.customers.FindAsync(new object[] { id }, cancellationToken);
        if (c == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        if (!await HasCustomerAccess(c, cancellationToken))
        {
            return Forbid();
        }

        _db.customers.Remove(c);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(true);
    }

    [HttpPut("{id}/assign")]
    public async Task<IActionResult> AssignCustomer(uint id, [FromBody] CustomerAssignDto dto, CancellationToken cancellationToken)
    {
        var role = User.GetUserRole();
        if (!User.CanManageAssignments())
        {
            return Forbid();
        }

        dto.role = AppRoles.Normalize(dto.role);
        if (dto.role != AppRoles.Sale && dto.role != AppRoles.TeleSale)
        {
            return BadRequest(new { message = "Assignment role must be Sale or Tele Sale." });
        }

        var c = await _db.customers.FindAsync(new object[] { id }, cancellationToken);
        if (c == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        if (!await HasCustomerAccess(c, cancellationToken))
        {
            return Forbid();
        }

        if (User.IsSupervisor())
        {
            if (dto.userId > 0)
            {
                var position = User.GetUserPosition();
                var targetUserInTeam = await _db.users.AnyAsync(
                    u => u.id == dto.userId &&
                         u.position == position &&
                         (dto.role == AppRoles.Sale
                             ? u.roles == AppRoles.Sale
                             : (u.roles == AppRoles.TeleSale || u.roles == "Tele sale")),
                    cancellationToken);
                if (!targetUserInTeam)
                {
                    return Forbid();
                }
            }
        }
        else if (dto.userId > 0)
        {
            var targetUserExists = await _db.users.AnyAsync(
                u => u.id == dto.userId &&
                     (u.is_active == null || u.is_active == true) &&
                     (dto.role == AppRoles.Sale
                         ? u.roles == AppRoles.Sale
                         : (u.roles == AppRoles.TeleSale || u.roles == "Tele sale")),
                cancellationToken);
            if (!targetUserExists)
            {
                return BadRequest(new { message = "Invalid assignment target for the selected role." });
            }
        }

        if (dto.role == AppRoles.Sale)
        {
            c.sale_id = dto.userId == 0 ? null : dto.userId;
            c.is_assign_sale = dto.userId != 0;
        }
        else
        {
            c.telesale_id = dto.userId == 0 ? null : dto.userId;
            c.is_assign_telesale = dto.userId != 0;
        }

        if (c.status == "New" && (c.sale_id.HasValue || c.telesale_id.HasValue))
        {
            c.status = "Assigned";
        }

        c.updated_at = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await MapToResponseDto(c, cancellationToken));
    }

    [HttpPut("{id}/book")]
    public async Task<IActionResult> BookCustomer(uint id, CancellationToken cancellationToken)
    {
        var role = User.GetUserRole();
        if (!User.CanWriteCustomerWorkflow()) return Forbid();

        var c = await _db.customers.FindAsync(new object[] { id }, cancellationToken);
        if (c == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        if (!await HasCustomerAccess(c, cancellationToken))
        {
            return Forbid();
        }

        c.status = "Booking";
        c.updated_at = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await MapToResponseDto(c, cancellationToken));
    }

    // Contact Details Sub-Endpoints
    [HttpGet("{id}/contacts")]
    public async Task<IActionResult> GetContacts(uint id, CancellationToken cancellationToken)
    {
        var c = await _db.customers.FindAsync(new object[] { id }, cancellationToken);
        if (c == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        if (!await HasCustomerAccess(c, cancellationToken))
        {
            return Forbid();
        }

        var list = await _db.details
            .AsNoTracking()
            .Where(d => d.cust_id == id && (d.is_active == null || d.is_active == true))
            .Select(d => new ContactResponseDto
            {
                id = d.id,
                cust_id = d.cust_id,
                contact_name = d.contact_name ?? "",
                contact_email = d.contact_email ?? "",
                contact_tel = d.contact_tel ?? "",
                contact_tel_office = d.contact_tel_office ?? "",
                point = d.point,
                total_point = d.total_point
            })
            .ToListAsync(cancellationToken);
        return Ok(list);
    }

    [HttpPost("{id}/contacts")]
    public async Task<IActionResult> AddContact(uint id, [FromBody] ContactCreateDto dto, CancellationToken cancellationToken)
    {
        var role = User.GetUserRole();
        if (!User.CanWriteCustomerWorkflow()) return Forbid();

        var c = await _db.customers.FindAsync(new object[] { id }, cancellationToken);
        if (c == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        if (!await HasCustomerAccess(c, cancellationToken))
        {
            return Forbid();
        }

        var d = new detail
        {
            cust_id = id,
            contact_name = dto.contact_name,
            contact_email = dto.contact_email ?? "",
            contact_tel = dto.contact_tel ?? "",
            contact_tel_office = dto.contact_tel_office ?? "",
            is_active = true,
            point = 0,
            total_point = 0,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        };
        _db.details.Add(d);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new ContactResponseDto
        {
            id = d.id,
            cust_id = d.cust_id,
            contact_name = d.contact_name,
            contact_email = d.contact_email,
            contact_tel = d.contact_tel,
            contact_tel_office = d.contact_tel_office,
            point = d.point,
            total_point = d.total_point
        });
    }

    [HttpPut("contacts/{id}")]
    public async Task<IActionResult> UpdateContact(uint id, [FromBody] ContactUpdateDto dto, CancellationToken cancellationToken)
    {
        var role = User.GetUserRole();
        if (!User.CanWriteCustomerWorkflow()) return Forbid();

        var d = await _db.details.FindAsync(new object[] { id }, cancellationToken);
        if (d == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        var c = await _db.customers.FindAsync(new object[] { d.cust_id }, cancellationToken);
        if (c == null || !await HasCustomerAccess(c, cancellationToken))
        {
            return Forbid();
        }

        if (dto.contact_name != null) d.contact_name = dto.contact_name;
        if (dto.contact_email != null) d.contact_email = dto.contact_email;
        if (dto.contact_tel != null) d.contact_tel = dto.contact_tel;
        if (dto.contact_tel_office != null) d.contact_tel_office = dto.contact_tel_office;

        d.updated_at = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new ContactResponseDto
        {
            id = d.id,
            cust_id = d.cust_id,
            contact_name = d.contact_name ?? "",
            contact_email = d.contact_email ?? "",
            contact_tel = d.contact_tel ?? "",
            contact_tel_office = d.contact_tel_office ?? "",
            point = d.point,
            total_point = d.total_point
        });
    }

    [HttpDelete("contacts/{id}")]
    public async Task<IActionResult> DeleteContact(uint id, CancellationToken cancellationToken)
    {
        var role = User.GetUserRole();
        if (!User.CanWriteCustomerWorkflow()) return Forbid();

        var d = await _db.details.FindAsync(new object[] { id }, cancellationToken);
        if (d == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        var c = await _db.customers.FindAsync(new object[] { d.cust_id }, cancellationToken);
        if (c == null || !await HasCustomerAccess(c, cancellationToken))
        {
            return Forbid();
        }

        var devices = await _db.detail_devices.Where(x => x.dtl_id == id).ToListAsync(cancellationToken);
        _db.detail_devices.RemoveRange(devices);

        var projects = await _db.detail_pjs.Where(x => x.dtl_id == id).ToListAsync(cancellationToken);
        _db.detail_pjs.RemoveRange(projects);

        _db.details.Remove(d);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(true);
    }

    // Devices & Projects
    [HttpGet("contacts/{contactId}/devices")]
    public async Task<IActionResult> GetDevices(uint contactId, CancellationToken cancellationToken)
    {
        var d = await _db.details.FindAsync(new object[] { contactId }, cancellationToken);
        if (d == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        var c = await _db.customers.FindAsync(new object[] { d.cust_id }, cancellationToken);
        if (c == null || !await HasCustomerAccess(c, cancellationToken))
        {
            return Forbid();
        }

        var list = await _db.detail_devices
            .AsNoTracking()
            .Where(dd => dd.dtl_id == contactId)
            .ToListAsync(cancellationToken);

        var competitors = await _db.competitors.AsNoTracking().ToDictionaryAsync(comp => (int)comp.id, comp => comp.name, cancellationToken);
        var result = list.Select(dd => new DeviceResponseDto
        {
            id = dd.id,
            dtl_id = dd.dtl_id,
            full_name = dd.equipment_dtl ?? "",
            full_name2 = "",
            dtl = dd.equipment_dtl ?? "",
            desktop_qty = dd.desktop_qty ?? 0,
            server_qty = dd.server_qty ?? 0,
            equipment_expire = dd.equipment_expire?.ToString("yyyy-MM-dd"),
            point = dd.point,
            progress_status = dd.progress_status ?? "New",
            competitor_name = dd.competitor_id.HasValue && competitors.TryGetValue(dd.competitor_id.Value, out var compName) ? compName : ""
        }).ToList();

        return Ok(result);
    }

    [HttpPost("contacts/{contactId}/devices")]
    public async Task<IActionResult> AddDevice(uint contactId, [FromBody] DeviceCreateDto dto, CancellationToken cancellationToken)
    {
        var role = User.GetUserRole();
        if (!User.CanWriteCustomerWorkflow()) return Forbid();

        var d = await _db.details.FindAsync(new object[] { contactId }, cancellationToken);
        if (d == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        var c = await _db.customers.FindAsync(new object[] { d.cust_id }, cancellationToken);
        if (c == null || !await HasCustomerAccess(c, cancellationToken))
        {
            return Forbid();
        }

        int? compId = null;
        string competitorName = "";
        if (!string.IsNullOrEmpty(dto.competitor_name))
        {
            var comp = await _db.competitors.AsNoTracking().FirstOrDefaultAsync(comp => comp.name == dto.competitor_name, cancellationToken);
            if (comp != null)
            {
                compId = (int)comp.id;
                competitorName = comp.name;
            }
        }

        var dd = new detail_device
        {
            dtl_id = contactId,
            equipment_dtl = dto.full_name,
            desktop_qty = dto.desktop_qty,
            server_qty = dto.server_qty,
            equipment_expire = string.IsNullOrEmpty(dto.equipment_expire) ? null : DateOnly.Parse(dto.equipment_expire),
            point = dto.point,
            progress_status = dto.progress_status,
            competitor_id = compId,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        };
        _db.detail_devices.Add(dd);
        await _db.SaveChangesAsync(cancellationToken);

        await RecalculatePoints(contactId, cancellationToken);

        return Ok(new DeviceResponseDto
        {
            id = dd.id,
            dtl_id = dd.dtl_id,
            full_name = dd.equipment_dtl ?? "",
            full_name2 = "",
            dtl = dd.equipment_dtl ?? "",
            desktop_qty = dd.desktop_qty ?? 0,
            server_qty = dd.server_qty ?? 0,
            equipment_expire = dd.equipment_expire?.ToString("yyyy-MM-dd"),
            point = dd.point,
            progress_status = dd.progress_status ?? "New",
            competitor_name = competitorName
        });
    }

    [HttpPut("devices/{id}")]
    public async Task<IActionResult> UpdateDevice(uint id, [FromBody] DeviceUpdateDto dto, CancellationToken cancellationToken)
    {
        var role = User.GetUserRole();
        if (!User.CanWriteCustomerWorkflow()) return Forbid();

        var dd = await _db.detail_devices.FindAsync(new object[] { id }, cancellationToken);
        if (dd == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        var d = await _db.details.FindAsync(new object[] { dd.dtl_id }, cancellationToken);
        if (d == null) return Forbid();

        var c = await _db.customers.FindAsync(new object[] { d.cust_id }, cancellationToken);
        if (c == null || !await HasCustomerAccess(c, cancellationToken))
        {
            return Forbid();
        }

        if (dto.full_name != null) dd.equipment_dtl = dto.full_name;
        if (dto.desktop_qty.HasValue) dd.desktop_qty = dto.desktop_qty.Value;
        if (dto.server_qty.HasValue) dd.server_qty = dto.server_qty.Value;
        if (dto.equipment_expire != null) dd.equipment_expire = string.IsNullOrEmpty(dto.equipment_expire) ? null : DateOnly.Parse(dto.equipment_expire);
        if (dto.point.HasValue) dd.point = dto.point.Value;
        if (dto.progress_status != null) dd.progress_status = dto.progress_status;

        string competitorName = "";
        if (dto.competitor_name != null)
        {
            if (string.IsNullOrEmpty(dto.competitor_name))
            {
                dd.competitor_id = null;
            }
            else
            {
                var comp = await _db.competitors.AsNoTracking().FirstOrDefaultAsync(comp => comp.name == dto.competitor_name, cancellationToken);
                if (comp != null)
                {
                    dd.competitor_id = (int)comp.id;
                    competitorName = comp.name;
                }
            }
        }
        else if (dd.competitor_id.HasValue)
        {
            competitorName = await _db.competitors.AsNoTracking().Where(comp => comp.id == dd.competitor_id.Value).Select(comp => comp.name).FirstOrDefaultAsync(cancellationToken) ?? "";
        }

        dd.updated_at = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await RecalculatePoints(dd.dtl_id, cancellationToken);

        return Ok(new DeviceResponseDto
        {
            id = dd.id,
            dtl_id = dd.dtl_id,
            full_name = dd.equipment_dtl ?? "",
            full_name2 = "",
            dtl = dd.equipment_dtl ?? "",
            desktop_qty = dd.desktop_qty ?? 0,
            server_qty = dd.server_qty ?? 0,
            equipment_expire = dd.equipment_expire?.ToString("yyyy-MM-dd"),
            point = dd.point,
            progress_status = dd.progress_status ?? "New",
            competitor_name = competitorName
        });
    }

    [HttpDelete("devices/{id}")]
    public async Task<IActionResult> DeleteDevice(uint id, CancellationToken cancellationToken)
    {
        var role = User.GetUserRole();
        if (!User.CanWriteCustomerWorkflow()) return Forbid();

        var dd = await _db.detail_devices.FindAsync(new object[] { id }, cancellationToken);
        if (dd == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        var d = await _db.details.FindAsync(new object[] { dd.dtl_id }, cancellationToken);
        if (d == null) return Forbid();

        var c = await _db.customers.FindAsync(new object[] { d.cust_id }, cancellationToken);
        if (c == null || !await HasCustomerAccess(c, cancellationToken))
        {
            return Forbid();
        }

        uint dtlId = dd.dtl_id;
        _db.detail_devices.Remove(dd);
        await _db.SaveChangesAsync(cancellationToken);

        await RecalculatePoints(dtlId, cancellationToken);

        return Ok(true);
    }

    [HttpGet("contacts/{contactId}/projects")]
    public async Task<IActionResult> GetProjects(uint contactId, CancellationToken cancellationToken)
    {
        var d = await _db.details.FindAsync(new object[] { contactId }, cancellationToken);
        if (d == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        var c = await _db.customers.FindAsync(new object[] { d.cust_id }, cancellationToken);
        if (c == null || !await HasCustomerAccess(c, cancellationToken))
        {
            return Forbid();
        }

        var list = await _db.detail_pjs
            .AsNoTracking()
            .Where(dp => dp.dtl_id == contactId)
            .Select(dp => new ProjectResponseDto
            {
                id = dp.id,
                dtl_id = dp.dtl_id,
                dtl = dp.dtl ?? "",
                close_date = dp.close_date.HasValue ? dp.close_date.Value.ToString("yyyy-MM-dd") : null,
                point = dp.point,
                progress_status = dp.progress_status
            })
            .ToListAsync(cancellationToken);
        return Ok(list);
    }

    [HttpPost("contacts/{contactId}/projects")]
    public async Task<IActionResult> AddProject(uint contactId, [FromBody] ProjectCreateDto dto, CancellationToken cancellationToken)
    {
        var role = User.GetUserRole();
        if (!User.CanWriteCustomerWorkflow()) return Forbid();

        var d = await _db.details.FindAsync(new object[] { contactId }, cancellationToken);
        if (d == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        var c = await _db.customers.FindAsync(new object[] { d.cust_id }, cancellationToken);
        if (c == null || !await HasCustomerAccess(c, cancellationToken))
        {
            return Forbid();
        }

        var dp = new detail_pj
        {
            dtl_id = contactId,
            dtl = dto.dtl,
            close_date = string.IsNullOrEmpty(dto.close_date) ? null : DateOnly.Parse(dto.close_date),
            point = dto.point,
            progress_status = dto.progress_status,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        };
        _db.detail_pjs.Add(dp);
        await _db.SaveChangesAsync(cancellationToken);

        await RecalculatePoints(contactId, cancellationToken);

        return Ok(new ProjectResponseDto
        {
            id = dp.id,
            dtl_id = dp.dtl_id,
            dtl = dp.dtl ?? "",
            close_date = dp.close_date.HasValue ? dp.close_date.Value.ToString("yyyy-MM-dd") : null,
            point = dp.point,
            progress_status = dp.progress_status
        });
    }

    [HttpPut("projects/{id}")]
    public async Task<IActionResult> UpdateProject(uint id, [FromBody] ProjectUpdateDto dto, CancellationToken cancellationToken)
    {
        if (!User.CanWriteCustomerWorkflow()) return Forbid();

        var dp = await _db.detail_pjs.FindAsync(new object[] { id }, cancellationToken);
        if (dp == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        var d = await _db.details.FindAsync(new object[] { dp.dtl_id }, cancellationToken);
        if (d == null) return Forbid();

        var c = await _db.customers.FindAsync(new object[] { d.cust_id }, cancellationToken);
        if (c == null || !await HasCustomerAccess(c, cancellationToken))
        {
            return Forbid();
        }

        if (dto.dtl != null) dp.dtl = dto.dtl;
        if (dto.close_date != null) dp.close_date = string.IsNullOrEmpty(dto.close_date) ? null : DateOnly.Parse(dto.close_date);
        if (dto.point.HasValue) dp.point = dto.point.Value;
        if (dto.progress_status != null) dp.progress_status = dto.progress_status;

        dp.updated_at = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await RecalculatePoints(dp.dtl_id, cancellationToken);

        return Ok(new ProjectResponseDto
        {
            id = dp.id,
            dtl_id = dp.dtl_id,
            dtl = dp.dtl ?? "",
            close_date = dp.close_date.HasValue ? dp.close_date.Value.ToString("yyyy-MM-dd") : null,
            point = dp.point,
            progress_status = dp.progress_status
        });
    }

    [HttpDelete("projects/{id}")]
    public async Task<IActionResult> DeleteProject(uint id, CancellationToken cancellationToken)
    {
        if (!User.CanWriteCustomerWorkflow()) return Forbid();

        var dp = await _db.detail_pjs.FindAsync(new object[] { id }, cancellationToken);
        if (dp == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        var d = await _db.details.FindAsync(new object[] { dp.dtl_id }, cancellationToken);
        if (d == null) return Forbid();

        var c = await _db.customers.FindAsync(new object[] { d.cust_id }, cancellationToken);
        if (c == null || !await HasCustomerAccess(c, cancellationToken))
        {
            return Forbid();
        }

        uint dtlId = dp.dtl_id;
        _db.detail_pjs.Remove(dp);
        await _db.SaveChangesAsync(cancellationToken);

        await RecalculatePoints(dtlId, cancellationToken);

        return Ok(true);
    }

    [HttpGet("reports/all")]
    public async Task<IActionResult> GetReportsAll(CancellationToken cancellationToken)
    {
        var role = User.GetUserRole();
        if (!User.CanReadReports())
        {
            return Forbid();
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        
        IQueryable<user> usersQuery = _db.users.AsNoTracking().Where(u => u.is_active == null || u.is_active == true);
        IQueryable<customer> customersQuery = _db.customers.AsNoTracking();

        if (User.IsSupervisor())
        {
            var position = User.GetUserPosition();
            usersQuery = usersQuery.Where(u => u.position == position && !string.IsNullOrEmpty(position));
        }
        customersQuery = customersQuery.ApplyCustomerScope(User, _db);

        var customers = await customersQuery.ToListAsync(cancellationToken);
        var users = await usersQuery.ToListAsync(cancellationToken);

        var permittedCustIds = customers.Select(c => c.id).ToList();

        var contacts = await _db.details.AsNoTracking()
            .Where(d => permittedCustIds.Contains(d.cust_id))
            .ToListAsync(cancellationToken);

        var permittedContactIds = contacts.Select(d => d.id).ToList();

        var devices = await _db.detail_devices.AsNoTracking()
            .Where(dd => permittedContactIds.Contains(dd.dtl_id))
            .ToListAsync(cancellationToken);

        var projects = await _db.detail_pjs.AsNoTracking()
            .Where(dp => permittedContactIds.Contains(dp.dtl_id))
            .ToListAsync(cancellationToken);

        var projectLedger = new List<object>();
        foreach (var con in contacts)
        {
            var cust = customers.FirstOrDefault(c => c.id == con.cust_id);
            if (cust == null) continue;

            var conProjs = projects.Where(p => p.dtl_id == con.id);
            foreach (var p in conProjs)
            {
                projectLedger.Add(new
                {
                    customerName = cust.name ?? "",
                    contactName = con.contact_name ?? "",
                    details = p.dtl ?? "",
                    closeDate = p.close_date?.ToString("yyyy-MM-dd") ?? "-",
                    points = p.point,
                    status = p.progress_status ?? "New",
                    type = "Project"
                });
            }

            var conDevs = devices.Where(d => d.dtl_id == con.id);
            foreach (var d in conDevs)
            {
                projectLedger.Add(new
                {
                    customerName = cust.name ?? "",
                    contactName = con.contact_name ?? "",
                    details = $"{d.equipment_dtl} ({d.desktop_qty ?? 0} Desktops, {d.server_qty ?? 0} Servers)",
                    closeDate = d.equipment_expire?.ToString("yyyy-MM-dd") ?? "-",
                    points = d.point,
                    status = d.progress_status ?? "New",
                    type = "Device License"
                });
            }
        }

        var agentPerformance = new List<object>();
        var activeAgents = users.Where(u => AppRoles.IsAgentRole(AppRoles.Normalize(u.roles)));
        foreach (var agent in activeAgents)
        {
            var agentRole = AppRoles.Normalize(agent.roles);
            var assignedCusts = customers.Where(c => agentRole == AppRoles.Sale ? c.sale_id == agent.id : c.telesale_id == agent.id).ToList();
            
            int totalPoints = 0;
            foreach (var c in assignedCusts)
            {
                var custContacts = contacts.Where(con => con.cust_id == c.id);
                foreach (var con in custContacts)
                {
                    totalPoints += con.point;
                }
            }

            int wins = assignedCusts.Count(c => c.status == "Win");
            int bookings = assignedCusts.Count(c => c.status == "Booking");
            int targetPoints = 150;
            int progress = (int)Math.Min(100, Math.Round(((double)totalPoints / targetPoints) * 100));

            agentPerformance.Add(new
            {
                id = agent.id,
                name = agent.name,
                role = agent.roles,
                points = totalPoints,
                wins = wins,
                bookings = bookings,
                progress = progress
            });
        }

        return Ok(new
        {
            projectLedger,
            agentPerformance
        });
    }

    private async Task RecalculatePoints(uint dtlId, CancellationToken cancellationToken)
    {
        var contact = await _db.details.FindAsync(new object[] { dtlId }, cancellationToken);
        if (contact != null)
        {
            var devicePoints = await _db.detail_devices.Where(dd => dd.dtl_id == dtlId).SumAsync(dd => dd.point, cancellationToken);
            var projectPoints = await _db.detail_pjs.Where(dp => dp.dtl_id == dtlId).SumAsync(dp => dp.point, cancellationToken);

            contact.point = devicePoints + projectPoints;
            contact.total_point = (int)((devicePoints + projectPoints) * 1.5);
            
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<CustomerResponseDto> MapToResponseDto(customer c, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        string btType = "Others";
        if (c.business_type_id.HasValue)
        {
            btType = await _db.business_types.AsNoTracking().Where(bt => bt.id == c.business_type_id.Value).Select(bt => bt.type).FirstOrDefaultAsync(cancellationToken) ?? "Others";
        }

        string? saleName = null;
        if (c.sale_id.HasValue)
        {
            saleName = await _db.users.AsNoTracking().Where(u => u.id == c.sale_id.Value).Select(u => u.name).FirstOrDefaultAsync(cancellationToken);
        }

        string? teleName = null;
        if (c.telesale_id.HasValue)
        {
            teleName = await _db.users.AsNoTracking().Where(u => u.id == c.telesale_id.Value).Select(u => u.name).FirstOrDefaultAsync(cancellationToken);
        }

        int renewalDays = 30;
        if (c.start_dt.HasValue)
        {
            var expiryDate = c.start_dt.Value.AddYears(1);
            renewalDays = expiryDate.DayNumber - today.DayNumber;
        }

        bool hasCostSheet = false;
        if (!string.IsNullOrEmpty(c.name))
        {
            hasCostSheet = await _db.cost_sheets.AsNoTracking().AnyAsync(cs => cs.company == c.name, cancellationToken);
        }

        return new CustomerResponseDto
        {
            id = c.id,
            name = c.name ?? "Unnamed",
            address = c.address ?? "",
            capital = c.capital?.ToString() ?? "0",
            telesale_id = c.telesale_id,
            telesale = teleName,
            sale_id = c.sale_id,
            sale = saleName,
            status = c.status,
            is_active = c.is_active ?? true,
            start_dt = c.start_dt?.ToString("yyyy-MM-dd"),
            bt_type = btType,
            renewalDays = renewalDays,
            hasCostSheet = hasCostSheet,
            updatedAt = c.updated_at?.ToString("yyyy-MM-dd") ?? c.created_at?.ToString("yyyy-MM-dd") ?? ""
        };
    }

    private async Task<bool> HasCustomerAccess(customer c, CancellationToken cancellationToken = default)
    {
        return await User.HasCustomerAccessAsync(c, _db, cancellationToken);
    }
}

public class CustomerCreateDto
{
    [Required]
    [StringLength(255)]
    public string name { get; set; } = null!;
    
    [StringLength(255)]
    public string address { get; set; } = "";
    
    [StringLength(255)]
    public string phone { get; set; } = "";
    
    [RegularExpression(@"^[0-9,]*(\.[0-9]+)?$", ErrorMessage = "Capital must be a valid number.")]
    public string capital { get; set; } = "0";
    
    [Required]
    [StringLength(255)]
    public string bt_type { get; set; } = null!;
}

public class CustomerUpdateDto
{
    [StringLength(255)]
    public string? name { get; set; }
    
    [StringLength(255)]
    public string? address { get; set; }
    
    [StringLength(255)]
    public string? phone { get; set; }
    
    [RegularExpression(@"^[0-9,]*(\.[0-9]+)?$", ErrorMessage = "Capital must be a valid number.")]
    public string? capital { get; set; }
    
    public bool? is_active { get; set; }
    
    [StringLength(255)]
    public string? status { get; set; }
    
    [StringLength(255)]
    public string? bt_type { get; set; }
}

public class CustomerAssignDto
{
    [Range(0, int.MaxValue)]
    public int userId { get; set; }
    
    [Required]
    [StringLength(255)]
    public string role { get; set; } = null!;
}

public class ContactCreateDto
{
    [Required]
    [StringLength(255)]
    public string contact_name { get; set; } = null!;
    
    [EmailAddress]
    [StringLength(255)]
    public string contact_email { get; set; } = "";
    
    [StringLength(255)]
    public string contact_tel { get; set; } = "";
    
    [StringLength(255)]
    public string contact_tel_office { get; set; } = "";
}

public class ContactUpdateDto
{
    [StringLength(255)]
    public string? contact_name { get; set; }
    
    [EmailAddress]
    [StringLength(255)]
    public string? contact_email { get; set; }
    
    [StringLength(255)]
    public string? contact_tel { get; set; }
    
    [StringLength(255)]
    public string? contact_tel_office { get; set; }
}

public class DeviceCreateDto
{
    [Required]
    [StringLength(255)]
    public string full_name { get; set; } = null!;
    
    [Range(0, 1000000)]
    public int desktop_qty { get; set; }
    
    [Range(0, 1000000)]
    public int server_qty { get; set; }
    
    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "Date must be in yyyy-MM-dd format.")]
    public string? equipment_expire { get; set; }
    
    [Range(0, 100000)]
    public int point { get; set; }
    
    [Required]
    [StringLength(255)]
    public string progress_status { get; set; } = "New";
    
    [StringLength(255)]
    public string? competitor_name { get; set; }
}

public class DeviceUpdateDto
{
    [StringLength(255)]
    public string? full_name { get; set; }
    
    [Range(0, 1000000)]
    public int? desktop_qty { get; set; }
    
    [Range(0, 1000000)]
    public int? server_qty { get; set; }
    
    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "Date must be in yyyy-MM-dd format.")]
    public string? equipment_expire { get; set; }
    
    [Range(0, 100000)]
    public int? point { get; set; }
    
    [StringLength(255)]
    public string? progress_status { get; set; }
    
    [StringLength(255)]
    public string? competitor_name { get; set; }
}

public class ProjectCreateDto
{
    [Required]
    [StringLength(255)]
    public string dtl { get; set; } = null!;
    
    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "Date must be in yyyy-MM-dd format.")]
    public string? close_date { get; set; }
    
    [Range(0, 100000)]
    public int point { get; set; }
    
    [Required]
    [StringLength(255)]
    public string progress_status { get; set; } = "New";
}

public class ProjectUpdateDto
{
    [StringLength(255)]
    public string? dtl { get; set; }
    
    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "Date must be in yyyy-MM-dd format.")]
    public string? close_date { get; set; }
    
    [Range(0, 100000)]
    public int? point { get; set; }
    
    [StringLength(255)]
    public string? progress_status { get; set; }
}
