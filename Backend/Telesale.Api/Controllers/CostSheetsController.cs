using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;
using Telesale.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Telesale.Api.Helpers;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;

namespace Telesale.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CostSheetsController : ControllerBase
{
    private readonly TelesaleDbContext _db;

    public CostSheetsController(TelesaleDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetCostSheets(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        if (!User.CanReadManagementData()) return Forbid();

        IQueryable<customer> customerQuery = _db.customers.ApplyCustomerScope(User, _db);

        var permittedCustomers = await customerQuery.AsNoTracking().Select(c => new { c.id, c.name }).ToListAsync(cancellationToken);
        var permittedCustIds = permittedCustomers.Select(c => (int)c.id).ToHashSet();
        var permittedCustNames = permittedCustomers.Select(c => c.name).Where(n => !string.IsNullOrEmpty(n)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var dbSheets = await _db.cost_sheets.AsNoTracking().ToListAsync(cancellationToken);

        if (!User.IsAdmin() && !User.IsViewer())
        {
            dbSheets = dbSheets.Where(cs =>
            {
                int custId = 0;
                if (!string.IsNullOrEmpty(cs.details))
                {
                    try
                    {
                        var detailsObj = JsonSerializer.Deserialize<CostSheetDetailsJson>(cs.details);
                        if (detailsObj != null && detailsObj.cust_id > 0)
                        {
                            custId = detailsObj.cust_id;
                        }
                    }
                    catch { }
                }

                if (custId > 0)
                {
                    return permittedCustIds.Contains(custId);
                }
                
                if (!string.IsNullOrEmpty(cs.company))
                {
                    return permittedCustNames.Contains(cs.company);
                }

                return false;
            }).ToList();
        }

        var customers = await _db.customers.AsNoTracking().ToDictionaryAsync(c => c.name ?? "", c => (int)c.id, cancellationToken);
        var brands = await _db.brands.AsNoTracking().ToDictionaryAsync(b => b.name ?? "", b => (int)b.id, cancellationToken);
        var products = await _db.products.AsNoTracking().ToDictionaryAsync(p => p.name ?? "", p => (int)p.id, cancellationToken);

        var list = dbSheets.Select(cs => MapToResponseDto(cs, customers, brands, products)).ToList();

        return Ok(list);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCostSheet([FromBody] CostSheetCreateDto dto, CancellationToken cancellationToken)
    {
        if (!User.CanWriteCustomerWorkflow()) return Forbid();

        var cust = await _db.customers.FindAsync(new object[] { (uint)dto.cust_id }, cancellationToken);
        if (cust == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        if (!await User.HasCustomerAccessAsync(cust, _db, cancellationToken))
        {
            return Forbid();
        }

        var brand = await _db.brands.FindAsync(new object[] { (uint)dto.brand_id }, cancellationToken);
        var prod = await _db.products.FindAsync(new object[] { (uint)dto.product_id }, cancellationToken);

        var detailsObj = new CostSheetDetailsJson
        {
            cust_id = dto.cust_id,
            project_name = dto.project_name,
            brand_id = dto.brand_id,
            product_id = dto.product_id,
            qty = dto.qty,
            cost_price = dto.cost_price,
            sale_price = dto.sale_price,
            discount = dto.discount,
            gp_amount = dto.gp_amount,
            gp_percent = dto.gp_percent,
            owner_share_percent = dto.owner_share_percent,
            employee_share_percent = dto.employee_share_percent
        };

        var cs = new cost_sheet
        {
            company = cust?.name ?? "",
            address = cust?.address ?? "",
            tel = cust?.phone ?? "",
            brand = brand?.name ?? "",
            edition = prod?.name ?? "",
            desktop_qty = dto.qty,
            margin = dto.margin,
            status = "Pending",
            is_active = true,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow,
            details = JsonSerializer.Serialize(detailsObj)
        };

        _db.cost_sheets.Add(cs);
        await _db.SaveChangesAsync(cancellationToken);

        var customers = await _db.customers.AsNoTracking().ToDictionaryAsync(c => c.name ?? "", c => (int)c.id, cancellationToken);
        var brands = await _db.brands.AsNoTracking().ToDictionaryAsync(b => b.name ?? "", b => (int)b.id, cancellationToken);
        var products = await _db.products.AsNoTracking().ToDictionaryAsync(p => p.name ?? "", p => (int)p.id, cancellationToken);

        return Ok(MapToResponseDto(cs, customers, brands, products));
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(uint id, [FromBody] CostSheetStatusUpdateDto dto, CancellationToken cancellationToken)
    {
        if (!User.CanManageAssignments())
        {
            return Forbid();
        }

        var cs = await _db.cost_sheets.FindAsync(new object[] { id }, cancellationToken);
        if (cs == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        if (User.IsSupervisor())
        {
            int custId = 0;
            if (!string.IsNullOrEmpty(cs.details))
            {
                try
                {
                    var detailsObj = JsonSerializer.Deserialize<CostSheetDetailsJson>(cs.details);
                    if (detailsObj != null) custId = detailsObj.cust_id;
                }
                catch { }
            }

            customer? cust = null;
            if (custId > 0)
            {
                cust = await _db.customers.FindAsync(new object[] { (uint)custId }, cancellationToken);
            }
            else if (!string.IsNullOrEmpty(cs.company))
            {
                cust = await _db.customers.FirstOrDefaultAsync(c => c.name == cs.company, cancellationToken);
            }

            if (cust == null)
            {
                return Forbid();
            }

            if (!await User.HasCustomerAccessAsync(cust, _db, cancellationToken))
            {
                return Forbid();
            }
        }

        cs.status = dto.status;
        cs.updated_at = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var customers = await _db.customers.AsNoTracking().ToDictionaryAsync(c => c.name ?? "", c => (int)c.id, cancellationToken);
        var brands = await _db.brands.AsNoTracking().ToDictionaryAsync(b => b.name ?? "", b => (int)b.id, cancellationToken);
        var products = await _db.products.AsNoTracking().ToDictionaryAsync(p => p.name ?? "", p => (int)p.id, cancellationToken);

        return Ok(MapToResponseDto(cs, customers, brands, products));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCostSheet(uint id, CancellationToken cancellationToken)
    {
        if (!User.CanManageAssignments())
        {
            return Forbid();
        }

        var cs = await _db.cost_sheets.FindAsync(new object[] { id }, cancellationToken);
        if (cs == null)
        {
            return User.IsAdmin() ? NotFound() : Forbid();
        }

        if (User.IsSupervisor())
        {
            int custId = 0;
            if (!string.IsNullOrEmpty(cs.details))
            {
                try
                {
                    var detailsObj = JsonSerializer.Deserialize<CostSheetDetailsJson>(cs.details);
                    if (detailsObj != null) custId = detailsObj.cust_id;
                }
                catch { }
            }

            customer? cust = null;
            if (custId > 0)
            {
                cust = await _db.customers.FindAsync(new object[] { (uint)custId }, cancellationToken);
            }
            else if (!string.IsNullOrEmpty(cs.company))
            {
                cust = await _db.customers.FirstOrDefaultAsync(c => c.name == cs.company, cancellationToken);
            }

            if (cust == null)
            {
                return Forbid();
            }

            if (!await User.HasCustomerAccessAsync(cust, _db, cancellationToken))
            {
                return Forbid();
            }
        }

        _db.cost_sheets.Remove(cs);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(true);
    }

    private CostSheetResponseDto MapToResponseDto(cost_sheet cs, Dictionary<string, int> customers, Dictionary<string, int> brands, Dictionary<string, int> products)
    {
        CostSheetDetailsJson? detailsObj = null;
        if (!string.IsNullOrEmpty(cs.details))
        {
            try
            {
                detailsObj = JsonSerializer.Deserialize<CostSheetDetailsJson>(cs.details);
            }
            catch { }
        }

        int custId = 0;
        if (detailsObj != null && detailsObj.cust_id > 0)
        {
            custId = detailsObj.cust_id;
        }
        else if (!string.IsNullOrEmpty(cs.company) && customers.TryGetValue(cs.company, out var cid))
        {
            custId = cid;
        }

        int brandId = 0;
        if (detailsObj != null && detailsObj.brand_id > 0)
        {
            brandId = detailsObj.brand_id;
        }
        else if (!string.IsNullOrEmpty(cs.brand) && brands.TryGetValue(cs.brand, out var bid))
        {
            brandId = bid;
        }

        int productId = 0;
        if (detailsObj != null && detailsObj.product_id > 0)
        {
            productId = detailsObj.product_id;
        }
        else if (!string.IsNullOrEmpty(cs.edition) && products.TryGetValue(cs.edition, out var pid))
        {
            productId = pid;
        }

        return new CostSheetResponseDto
        {
            id = cs.id,
            cust_id = custId,
            project_name = detailsObj?.project_name ?? cs.company ?? "Unnamed Project",
            brand_id = brandId,
            product_id = productId,
            qty = detailsObj?.qty ?? cs.desktop_qty,
            cost_price = detailsObj?.cost_price ?? 0,
            sale_price = detailsObj?.sale_price ?? 0,
            discount = detailsObj?.discount ?? 0,
            margin = cs.margin,
            gp_amount = detailsObj?.gp_amount ?? cs.margin,
            gp_percent = detailsObj?.gp_percent ?? 0.0,
            owner_share_percent = detailsObj?.owner_share_percent ?? 60,
            employee_share_percent = detailsObj?.employee_share_percent ?? 40,
            status = cs.status ?? "Pending",
            created_at = cs.created_at?.ToString("yyyy-MM-dd") ?? ""
        };
    }
}

public class CostSheetDetailsJson
{
    public int cust_id { get; set; }
    public string project_name { get; set; } = "";
    public int brand_id { get; set; }
    public int product_id { get; set; }
    public int qty { get; set; }
    public double cost_price { get; set; }
    public double sale_price { get; set; }
    public double discount { get; set; }
    public double gp_amount { get; set; }
    public double gp_percent { get; set; }
    public int owner_share_percent { get; set; }
    public int employee_share_percent { get; set; }
}

public class CostSheetCreateDto
{
    [Range(1, int.MaxValue)]
    public int cust_id { get; set; }
    
    [Required]
    [StringLength(255)]
    public string project_name { get; set; } = null!;
    
    [Range(1, int.MaxValue)]
    public int brand_id { get; set; }
    
    [Range(1, int.MaxValue)]
    public int product_id { get; set; }
    
    [Range(1, 1000000)]
    public int qty { get; set; }
    
    [Range(0, int.MaxValue)]
    public int cost_price { get; set; }
    
    [Range(0, int.MaxValue)]
    public int sale_price { get; set; }
    
    [Range(0, int.MaxValue)]
    public int discount { get; set; }
    
    [Range(0, int.MaxValue)]
    public int margin { get; set; }
    
    [Range(0, int.MaxValue)]
    public int gp_amount { get; set; }
    
    [Range(-100, 100)]
    public double gp_percent { get; set; }
    
    [Range(0, 100)]
    public int owner_share_percent { get; set; }
    
    [Range(0, 100)]
    public int employee_share_percent { get; set; }
}

public class CostSheetStatusUpdateDto
{
    [Required]
    [RegularExpression(@"^(Approved|Rejected|Pending)$", ErrorMessage = "Status must be Approved, Rejected, or Pending.")]
    public string status { get; set; } = null!;
}
