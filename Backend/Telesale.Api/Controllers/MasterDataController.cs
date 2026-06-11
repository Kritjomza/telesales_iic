using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;
using Telesale.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Telesale.Api.Helpers;
using System.ComponentModel.DataAnnotations;

namespace Telesale.Api.Controllers;

[Authorize]
[AdminWriteOnly]
[ApiController]
[Route("api/[controller]")]
public class MasterDataController : ControllerBase
{
    private readonly TelesaleDbContext _db;

    public MasterDataController(TelesaleDbContext db)
    {
        _db = db;
    }

    // BRANDS
    [HttpGet("brands")]
    public async Task<IActionResult> GetBrands(CancellationToken cancellationToken)
    {
        var list = await _db.brands.AsNoTracking().ToListAsync(cancellationToken);
        var result = list.Select(b => new
        {
            id = b.id,
            name = b.name ?? "",
            code = b.name != null && b.name.Length >= 3 ? b.name.Substring(0, 3).ToUpper() : "BRD"
        }).ToList();
        return Ok(result);
    }

    [HttpPost("brands")]
    public async Task<IActionResult> CreateBrand([FromBody] BrandDto dto, CancellationToken cancellationToken)
    {
        var b = new brand
        {
            name = dto.name,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        };
        _db.brands.Add(b);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            id = b.id,
            name = b.name,
            code = b.name.Length >= 3 ? b.name.Substring(0, 3).ToUpper() : "BRD"
        });
    }

    [HttpPut("brands/{id}")]
    public async Task<IActionResult> UpdateBrand(uint id, [FromBody] BrandDto dto, CancellationToken cancellationToken)
    {
        var b = await _db.brands.FindAsync(new object[] { id }, cancellationToken);
        if (b == null) return NotFound();

        b.name = dto.name;
        b.updated_at = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            id = b.id,
            name = b.name,
            code = b.name.Length >= 3 ? b.name.Substring(0, 3).ToUpper() : "BRD"
        });
    }

    [HttpDelete("brands/{id}")]
    public async Task<IActionResult> DeleteBrand(uint id, CancellationToken cancellationToken)
    {
        var b = await _db.brands.FindAsync(new object[] { id }, cancellationToken);
        if (b == null) return NotFound();

        _db.brands.Remove(b);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(true);
    }

    // PRODUCTS
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts(CancellationToken cancellationToken)
    {
        var products = await _db.products.AsNoTracking().ToListAsync(cancellationToken);
        var priceLists = await _db.antivirus_price_lists.AsNoTracking().ToListAsync(cancellationToken);

        var result = products.Select(p =>
        {
            var priceItem = priceLists.FirstOrDefault(pl => 
                pl.edition != null && p.name != null && pl.edition.Contains(p.name, StringComparison.OrdinalIgnoreCase));
            
            double cost = priceItem?.cost ?? 1200.0;
            double price = cost * 1.5;

            return new
            {
                id = p.id,
                brand_id = p.brands_id ?? 1,
                category_id = p.categories_id,
                name = p.name ?? "",
                cost = cost,
                price = price
            };
        }).ToList();
        return Ok(result);
    }

    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct([FromBody] ProductDto dto, CancellationToken cancellationToken)
    {
        var p = new product
        {
            name = dto.name,
            brands_id = (uint)dto.brand_id,
            categories_id = (uint)dto.category_id,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        };
        _db.products.Add(p);
        await _db.SaveChangesAsync(cancellationToken);

        var brandName = await _db.brands.AsNoTracking().Where(b => b.id == dto.brand_id).Select(b => b.name).FirstOrDefaultAsync(cancellationToken) ?? "Kaspersky";
        var apl = new antivirus_price_list
        {
            brand = brandName,
            code = brandName.Length >= 3 ? brandName.Substring(0, 3).ToUpper() : "KSP",
            edition = dto.name,
            cost = dto.cost > 0 ? dto.cost : 1200.0,
            start = 1,
            end = 100,
            types = "Client",
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        };
        _db.antivirus_price_lists.Add(apl);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            id = p.id,
            brand_id = p.brands_id,
            category_id = p.categories_id,
            name = p.name,
            cost = apl.cost,
            price = apl.cost * 1.5
        });
    }

    [HttpPut("products/{id}")]
    public async Task<IActionResult> UpdateProduct(uint id, [FromBody] ProductDto dto, CancellationToken cancellationToken)
    {
        var p = await _db.products.FindAsync(new object[] { id }, cancellationToken);
        if (p == null) return NotFound();

        p.name = dto.name;
        p.brands_id = (uint)dto.brand_id;
        p.categories_id = (uint)dto.category_id;
        p.updated_at = DateTime.UtcNow;

        var apl = await _db.antivirus_price_lists.FirstOrDefaultAsync(x => x.edition == p.name, cancellationToken);
        if (apl != null)
        {
            apl.cost = dto.cost;
            apl.updated_at = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            id = p.id,
            brand_id = p.brands_id,
            category_id = p.categories_id,
            name = p.name,
            cost = apl?.cost ?? dto.cost,
            price = (apl?.cost ?? dto.cost) * 1.5
        });
    }

    [HttpDelete("products/{id}")]
    public async Task<IActionResult> DeleteProduct(uint id, CancellationToken cancellationToken)
    {
        var p = await _db.products.FindAsync(new object[] { id }, cancellationToken);
        if (p == null) return NotFound();

        _db.products.Remove(p);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(true);
    }

    // ANTIVIRUS PRICE LISTS
    [HttpGet("antivirus-prices")]
    public async Task<IActionResult> GetAntivirusPrices(CancellationToken cancellationToken)
    {
        var priceLists = await _db.antivirus_price_lists.AsNoTracking().ToListAsync(cancellationToken);
        var brands = await _db.brands.AsNoTracking().ToListAsync(cancellationToken);

        var result = priceLists.Select(pl =>
        {
            var brandObj = brands.FirstOrDefault(b => 
                b.name != null && pl.brand != null && b.name.Equals(pl.brand, StringComparison.OrdinalIgnoreCase));
            
            return new
            {
                id = pl.id,
                brand_id = brandObj != null ? brandObj.id : 1,
                brand = pl.brand ?? "",
                code = pl.code ?? "",
                edition = pl.edition ?? "",
                start = pl.start,
                end = pl.end,
                cost = pl.cost,
                types = pl.types ?? "Client"
            };
        }).ToList();

        return Ok(result);
    }

    [HttpPost("antivirus-prices")]
    public async Task<IActionResult> CreateAntivirusPrice([FromBody] AntivirusPriceDto dto, CancellationToken cancellationToken)
    {
        var brandName = await _db.brands.AsNoTracking().Where(b => b.id == dto.brand_id).Select(b => b.name).FirstOrDefaultAsync(cancellationToken) ?? "Kaspersky";
        var apl = new antivirus_price_list
        {
            brand = brandName,
            code = brandName.Length >= 3 ? brandName.Substring(0, 3).ToUpper() : "KSP",
            edition = dto.edition,
            cost = dto.price,
            start = dto.start > 0 ? dto.start : 1,
            end = dto.end > 0 ? dto.end : 100,
            types = dto.types,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        };
        _db.antivirus_price_lists.Add(apl);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            id = apl.id,
            brand_id = dto.brand_id,
            brand = apl.brand,
            code = apl.code,
            edition = apl.edition,
            start = apl.start,
            end = apl.end,
            cost = apl.cost,
            types = apl.types
        });
    }

    [HttpPut("antivirus-prices/{id}")]
    public async Task<IActionResult> UpdateAntivirusPrice(uint id, [FromBody] AntivirusPriceDto dto, CancellationToken cancellationToken)
    {
        var apl = await _db.antivirus_price_lists.FindAsync(new object[] { id }, cancellationToken);
        if (apl == null) return NotFound();

        var brandName = await _db.brands.AsNoTracking().Where(b => b.id == dto.brand_id).Select(b => b.name).FirstOrDefaultAsync(cancellationToken) ?? "Kaspersky";
        apl.brand = brandName;
        apl.code = brandName.Length >= 3 ? brandName.Substring(0, 3).ToUpper() : "KSP";
        apl.edition = dto.edition;
        apl.cost = dto.price;
        apl.start = dto.start > 0 ? dto.start : 1;
        apl.end = dto.end > 0 ? dto.end : 100;
        apl.types = dto.types;
        apl.updated_at = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            id = apl.id,
            brand_id = dto.brand_id,
            brand = apl.brand,
            code = apl.code,
            edition = apl.edition,
            start = apl.start,
            end = apl.end,
            cost = apl.cost,
            types = apl.types
        });
    }

    [HttpDelete("antivirus-prices/{id}")]
    public async Task<IActionResult> DeleteAntivirusPrice(uint id, CancellationToken cancellationToken)
    {
        var apl = await _db.antivirus_price_lists.FindAsync(new object[] { id }, cancellationToken);
        if (apl == null) return NotFound();

        _db.antivirus_price_lists.Remove(apl);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(true);
    }

    // BUSINESS TYPES
    [HttpGet("business-types")]
    public async Task<IActionResult> GetBusinessTypes(CancellationToken cancellationToken)
    {
        var list = await _db.business_types.AsNoTracking().ToListAsync(cancellationToken);
        var result = list.Select(bt => new
        {
            id = bt.id,
            type = bt.type ?? "",
            dtl = bt.dtl ?? ""
        }).ToList();
        return Ok(result);
    }

    [HttpPost("business-types")]
    public async Task<IActionResult> CreateBusinessType([FromBody] BusinessTypeDto dto, CancellationToken cancellationToken)
    {
        var bt = new business_type
        {
            type = dto.type,
            dtl = dto.dtl,
            is_active = true,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        };
        _db.business_types.Add(bt);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            id = bt.id,
            type = bt.type,
            dtl = bt.dtl
        });
    }

    [HttpPut("business-types/{id}")]
    public async Task<IActionResult> UpdateBusinessType(uint id, [FromBody] BusinessTypeDto dto, CancellationToken cancellationToken)
    {
        var bt = await _db.business_types.FindAsync(new object[] { id }, cancellationToken);
        if (bt == null) return NotFound();

        bt.type = dto.type;
        bt.dtl = dto.dtl;
        bt.updated_at = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            id = bt.id,
            type = bt.type,
            dtl = bt.dtl
        });
    }

    [HttpDelete("business-types/{id}")]
    public async Task<IActionResult> DeleteBusinessType(uint id, CancellationToken cancellationToken)
    {
        var bt = await _db.business_types.FindAsync(new object[] { id }, cancellationToken);
        if (bt == null) return NotFound();

        _db.business_types.Remove(bt);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(true);
    }

    // CATEGORIES
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var list = await _db.categories.AsNoTracking().ToListAsync(cancellationToken);
        var result = list.Select(c => new
        {
            id = c.id,
            name = c.name ?? ""
        }).ToList();
        return Ok(result);
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] CategoryDto dto, CancellationToken cancellationToken)
    {
        var c = new category
        {
            name = dto.name,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        };
        _db.categories.Add(c);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            id = c.id,
            name = c.name
        });
    }

    [HttpPut("categories/{id}")]
    public async Task<IActionResult> UpdateCategory(uint id, [FromBody] CategoryDto dto, CancellationToken cancellationToken)
    {
        var c = await _db.categories.FindAsync(new object[] { id }, cancellationToken);
        if (c == null) return NotFound();

        c.name = dto.name;
        c.updated_at = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            id = c.id,
            name = c.name
        });
    }

    [HttpDelete("categories/{id}")]
    public async Task<IActionResult> DeleteCategory(uint id, CancellationToken cancellationToken)
    {
        var c = await _db.categories.FindAsync(new object[] { id }, cancellationToken);
        if (c == null) return NotFound();

        _db.categories.Remove(c);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(true);
    }

    // COMPETITORS
    [HttpGet("competitors")]
    public async Task<IActionResult> GetCompetitors(CancellationToken cancellationToken)
    {
        var list = await _db.competitors.AsNoTracking().ToListAsync(cancellationToken);
        var result = list.Select(c => new
        {
            id = c.id,
            name = c.name ?? "",
            year = int.TryParse(c.year, out var y) ? y : DateTime.Today.Year,
            amt = c.amt,
            compare = c.compare == "Bigger" ? "Larger" : "Smaller"
        }).ToList();
        return Ok(result);
    }

    [HttpPost("competitors")]
    public async Task<IActionResult> CreateCompetitor([FromBody] CompetitorDto dto, CancellationToken cancellationToken)
    {
        var c = new competitor
        {
            name = dto.name,
            year = dto.year.ToString(),
            amt = dto.amt,
            compare = dto.compare == "Larger" ? "Bigger" : "Smaller",
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        };
        _db.competitors.Add(c);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            id = c.id,
            name = c.name,
            year = dto.year,
            amt = c.amt,
            compare = dto.compare
        });
    }

    [HttpPut("competitors/{id}")]
    public async Task<IActionResult> UpdateCompetitor(uint id, [FromBody] CompetitorDto dto, CancellationToken cancellationToken)
    {
        var c = await _db.competitors.FindAsync(new object[] { id }, cancellationToken);
        if (c == null) return NotFound();

        c.name = dto.name;
        c.year = dto.year.ToString();
        c.amt = dto.amt;
        c.compare = dto.compare == "Larger" ? "Bigger" : "Smaller";
        c.updated_at = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            id = c.id,
            name = c.name,
            year = dto.year,
            amt = c.amt,
            compare = dto.compare
        });
    }

    [HttpDelete("competitors/{id}")]
    public async Task<IActionResult> DeleteCompetitor(uint id, CancellationToken cancellationToken)
    {
        var c = await _db.competitors.FindAsync(new object[] { id }, cancellationToken);
        if (c == null) return NotFound();

        _db.competitors.Remove(c);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(true);
    }

    // PROFILES
    [HttpGet("profiles")]
    public async Task<IActionResult> GetProfiles(CancellationToken cancellationToken)
    {
        var list = await _db.profiles.AsNoTracking().ToListAsync(cancellationToken);
        var result = list.Select(p => new
        {
            id = p.id,
            name = p.name ?? "",
            type = p.type ?? "ANTIVIRUS",
            item = p.items ?? "",
            edition = p.editions ?? ""
        }).ToList();
        return Ok(result);
    }

    [HttpPost("profiles")]
    public async Task<IActionResult> CreateProfile([FromBody] ProfileDto dto, CancellationToken cancellationToken)
    {
        var p = new profile
        {
            name = dto.name,
            type = dto.type,
            items = dto.item,
            editions = dto.edition,
            is_active = true,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        };
        _db.profiles.Add(p);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            id = p.id,
            name = p.name,
            type = p.type,
            item = p.items,
            edition = p.editions
        });
    }

    [HttpPut("profiles/{id}")]
    public async Task<IActionResult> UpdateProfile(uint id, [FromBody] ProfileDto dto, CancellationToken cancellationToken)
    {
        var p = await _db.profiles.FindAsync(new object[] { id }, cancellationToken);
        if (p == null) return NotFound();

        p.name = dto.name;
        p.type = dto.type;
        p.items = dto.item;
        p.editions = dto.edition;
        p.updated_at = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new
        {
            id = p.id,
            name = p.name,
            type = p.type,
            item = p.items,
            edition = p.editions
        });
    }

    [HttpDelete("profiles/{id}")]
    public async Task<IActionResult> DeleteProfile(uint id, CancellationToken cancellationToken)
    {
        var p = await _db.profiles.FindAsync(new object[] { id }, cancellationToken);
        if (p == null) return NotFound();

        _db.profiles.Remove(p);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(true);
    }
}

public class BrandDto
{
    [Required]
    [StringLength(255)]
    public string name { get; set; } = null!;
}

public class ProductDto
{
    [Required]
    [StringLength(255)]
    public string name { get; set; } = null!;
    
    [Range(1, int.MaxValue)]
    public int brand_id { get; set; }
    
    [Range(1, int.MaxValue)]
    public int category_id { get; set; }
    
    [Range(0, double.MaxValue)]
    public double cost { get; set; }
}

public class AntivirusPriceDto
{
    [Range(1, int.MaxValue)]
    public int brand_id { get; set; }
    
    [Required]
    [StringLength(255)]
    public string edition { get; set; } = null!;
    
    [Range(0, double.MaxValue)]
    public double price { get; set; }
    
    [Range(1, int.MaxValue)]
    public int start { get; set; }
    
    [Range(1, int.MaxValue)]
    public int end { get; set; }
    
    [Required]
    [RegularExpression(@"^(Client|Server)$", ErrorMessage = "Type must be Client or Server.")]
    public string types { get; set; } = "Client";
}

public class BusinessTypeDto
{
    [Required]
    [StringLength(255)]
    public string type { get; set; } = null!;
    
    [StringLength(255)]
    public string dtl { get; set; } = "";
}

public class CategoryDto
{
    [Required]
    [StringLength(255)]
    public string name { get; set; } = null!;
}

public class CompetitorDto
{
    [Required]
    [StringLength(255)]
    public string name { get; set; } = null!;
    
    [Range(1900, 2100)]
    public int year { get; set; }
    
    [Range(0, double.MaxValue)]
    public double amt { get; set; }
    
    [Required]
    [RegularExpression(@"^(Larger|Smaller|Equal)$", ErrorMessage = "Compare must be Larger, Smaller, or Equal.")]
    public string compare { get; set; } = "Smaller";
}

public class ProfileDto
{
    [Required]
    [StringLength(255)]
    public string name { get; set; } = null!;
    
    [Required]
    [StringLength(255)]
    public string type { get; set; } = "ANTIVIRUS";
    
    [StringLength(255)]
    public string item { get; set; } = "";
    
    [StringLength(255)]
    public string edition { get; set; } = "";
}
