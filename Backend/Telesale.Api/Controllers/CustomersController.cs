using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;
using Telesale.Api.Models;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;

using Microsoft.AspNetCore.Authorization;
using Telesale.Api.Helpers;
using Telesale.Api.Services;

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
        [FromQuery] string? completeness,
        [FromQuery] string? missingField,
        CancellationToken cancellationToken,
        [FromQuery] string? businessType = null,
        [FromQuery] int? saleId = null,
        [FromQuery] int? telesaleId = null,
        [FromQuery] string? status = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        if (!User.CanReadManagementData()) return Forbid();

        IQueryable<customer> query = _db.customers.ApplyCustomerScope(User, _db);

        if (!string.IsNullOrEmpty(businessType))
        {
            query = query.Where(c => _db.business_types.Any(bt => bt.id == c.business_type_id && bt.type == businessType));
        }
        if (saleId.HasValue && saleId.Value > 0)
        {
            query = query.Where(c => c.sale_id == saleId.Value);
        }
        if (telesaleId.HasValue && telesaleId.Value > 0)
        {
            query = query.Where(c => c.telesale_id == telesaleId.Value);
        }
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!StatusPolicy.IsValidCustomerStatus(status))
            {
                return BadRequest(new { message = StatusPolicy.GetInvalidStatusMessage("Customer", status, StatusPolicy.CustomerStatuses) });
            }
            query = query.Where(c => c.status == status);
        }

        var searchTerm = CustomerSearch.NormalizeMultiToken(search);
        if (searchTerm != null)
        {
            return await SearchCustomers(query, searchTerm, page, pageSize, today, completeness, missingField, cancellationToken);
        }

        if (page.HasValue)
        {
            var size = pageSize ?? 25;
            if (size <= 0) return BadRequest("Page size must be greater than zero.");
            if (size > 100) return BadRequest("Page size cannot exceed 100.");
            if (page.Value <= 0) return BadRequest("Page number must be greater than zero.");

            var metrics = await CalculateMetricsAsync(query, today, cancellationToken);
            var filteredQuery = ApplyCompletenessFilters(query, completeness, missingField);

            var totalCount = await filteredQuery.CountAsync(cancellationToken);
            var totalPages = (int)Math.Ceiling((double)totalCount / size);

            var rawItems = await filteredQuery.AsNoTracking()
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
                    updatedAt = c.updated_at ?? c.created_at,
                    c.phone,
                    c.subdistrict,
                    c.district,
                    c.province,
                    c.postal_code,
                    primary_contact_name = _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_name).FirstOrDefault(),
                    primary_contact_tel = _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_tel).FirstOrDefault(),
                    primary_contact_email = _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_email).FirstOrDefault(),
                    primary_contact_tel_office = _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_tel_office).FirstOrDefault(),
                    hasProductLicenseInfo = _db.details.Any(d => d.cust_id == c.id && _db.detail_devices.Any(dd => dd.dtl_id == d.id))
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
                updatedAt = c.updatedAt?.ToString("yyyy-MM-dd") ?? "",
                phone = c.phone,
                subdistrict = c.subdistrict,
                district = c.district,
                province = c.province,
                postal_code = c.postal_code,
                primary_contact_name = c.primary_contact_name,
                primary_contact_tel = c.primary_contact_tel,
                primary_contact_email = c.primary_contact_email,
                primary_contact_tel_office = c.primary_contact_tel_office,
                hasProductLicenseInfo = c.hasProductLicenseInfo
            }).ToList();

            return Ok(new
            {
                items = list,
                totalCount,
                page = page.Value,
                pageSize = size,
                totalPages,
                metrics
            });
        }
        else
        {
            var filteredQuery = ApplyCompletenessFilters(query, completeness, missingField);
            var rawItems = await filteredQuery.AsNoTracking()
                .OrderBy(c => c.id)
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
                    updatedAt = c.updated_at ?? c.created_at,
                    c.phone,
                    c.subdistrict,
                    c.district,
                    c.province,
                    c.postal_code,
                    primary_contact_name = _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_name).FirstOrDefault(),
                    primary_contact_tel = _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_tel).FirstOrDefault(),
                    primary_contact_email = _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_email).FirstOrDefault(),
                    primary_contact_tel_office = _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_tel_office).FirstOrDefault(),
                    hasProductLicenseInfo = _db.details.Any(d => d.cust_id == c.id && _db.detail_devices.Any(dd => dd.dtl_id == d.id))
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
                updatedAt = c.updatedAt?.ToString("yyyy-MM-dd") ?? "",
                phone = c.phone,
                subdistrict = c.subdistrict,
                district = c.district,
                province = c.province,
                postal_code = c.postal_code,
                primary_contact_name = c.primary_contact_name,
                primary_contact_tel = c.primary_contact_tel,
                primary_contact_email = c.primary_contact_email,
                primary_contact_tel_office = c.primary_contact_tel_office,
                hasProductLicenseInfo = c.hasProductLicenseInfo
            }).ToList();

            return Ok(list);
        }
    }

    private async Task<IActionResult> SearchCustomers(
        IQueryable<customer> scopedQuery,
        MultiTokenSearchTerm searchTerm,
        int? page,
        int? pageSize,
        DateOnly today,
        string? completeness,
        string? missingField,
        CancellationToken cancellationToken)
    {
        var size = pageSize ?? (page.HasValue ? 25 : 100);
        if (size <= 0) return BadRequest("Page size must be greater than zero.");
        if (size > 100) return BadRequest("Page size cannot exceed 100.");
        if (page.HasValue && page.Value <= 0) return BadRequest("Page number must be greater than zero.");

        var metrics = page.HasValue ? await CalculateMetricsAsync(scopedQuery, today, cancellationToken) : null;
        var filteredQuery = ApplyCompletenessFilters(scopedQuery, completeness, missingField);

        var candidateLimit = page.HasValue
            ? Math.Min(1000, Math.Max(size * page.Value * 4, 200))
            : 1000;

        var predicate = BuildCustomerSearchPredicate(searchTerm);
        var directCandidates = await ProjectCustomerRows(filteredQuery.Where(predicate), candidateLimit, cancellationToken);

        var rowsById = directCandidates.ToDictionary(c => c.Customer.id);
        if (rowsById.Count < candidateLimit)
        {
            var fallbackRows = await ProjectCustomerRows(filteredQuery, candidateLimit, cancellationToken);
            foreach (var row in fallbackRows)
            {
                rowsById.TryAdd(row.Customer.id, row);
            }
        }

        var ids = rowsById.Keys.ToList();
        var contactRows = await _db.details
            .AsNoTracking()
            .Where(d => ids.Contains(d.cust_id))
            .Select(d => new
            {
                d.cust_id,
                d.contact_name,
                d.contact_tel,
                d.contact_email,
                d.contact_tel_office
            })
            .ToListAsync(cancellationToken);

        var contactsByCustomerId = contactRows
            .GroupBy(d => d.cust_id)
            .ToDictionary(
                g => g.Key,
                g => g.Select(d => new ContactSearchDocument(d.contact_name, d.contact_tel, d.contact_email)).ToList());

        var companyNames = rowsById.Values
            .Select(r => r.Customer.name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .ToList();

        var bookingRows = await _db.cost_sheets
            .AsNoTracking()
            .Where(cs => cs.company != null && cs.qo_no != null && companyNames.Contains(cs.company))
            .Select(cs => new { cs.company, cs.qo_no })
            .ToListAsync(cancellationToken);

        var bookingNumbersByCompany = bookingRows
            .Where(cs => !string.IsNullOrWhiteSpace(cs.company))
            .GroupBy(cs => cs.company!)
            .ToDictionary(
                g => g.Key,
                g => g.Select(cs => cs.qo_no).ToList());

        var rankedRows = rowsById.Values
            .Select(row =>
            {
                contactsByCustomerId.TryGetValue(row.Customer.id, out var contacts);
                bookingNumbersByCompany.TryGetValue(row.Customer.name ?? "", out var bookingNumbers);
                var document = new CustomerSearchDocument(
                    row.Customer,
                    row.BusinessType,
                    row.SaleName,
                    row.TelesaleName,
                    contacts,
                    bookingNumbers);
                var match = CustomerSearch.RankDocument(document, searchTerm);
                return new { row, match };
            })
            .Where(x => x.match != null)
            .OrderBy(x => x.match!.Rank)
            .ThenBy(x => x.row.Customer.id)
            .ToList();

        var totalCount = rankedRows.Count;
        var totalPages = page.HasValue ? (int)Math.Ceiling((double)totalCount / size) : 1;
        var pageItems = page.HasValue
            ? rankedRows.Skip((page.Value - 1) * size).Take(size).ToList()
            : rankedRows.Take(size).ToList();

        var list = pageItems
            .Select(x => MapCustomerListRow(x.row, today, x.match!.MatchedField))
            .ToList();

        if (page.HasValue)
        {
            return Ok(new
            {
                items = list,
                totalCount,
                page = page.Value,
                pageSize = size,
                totalPages,
                metrics
            });
        }

        return Ok(list);
    }

    private async Task<List<CustomerListRow>> ProjectCustomerRows(
        IQueryable<customer> query,
        int limit,
        CancellationToken cancellationToken)
    {
        var customers = await query
            .AsNoTracking()
            .OrderBy(c => c.id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        if (customers.Count == 0)
        {
            return new List<CustomerListRow>();
        }

        var customerIds = customers.Select(c => c.id).ToList();
        var businessTypeIds = customers
            .Where(c => c.business_type_id.HasValue && c.business_type_id.Value > 0)
            .Select(c => (uint)c.business_type_id!.Value)
            .Distinct()
            .ToList();
        var userIds = customers
            .SelectMany(c => new[] { c.sale_id, c.telesale_id })
            .Where(id => id.HasValue && id.Value > 0)
            .Select(id => (uint)id!.Value)
            .Distinct()
            .ToList();
        var companyNames = customers
            .Select(c => c.name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .ToList();

        var businessTypesById = await _db.business_types
            .AsNoTracking()
            .Where(bt => businessTypeIds.Contains(bt.id))
            .ToDictionaryAsync(bt => bt.id, bt => bt.type ?? "Others", cancellationToken);
        var usersById = await _db.users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.id))
            .ToDictionaryAsync(u => u.id, u => u.name, cancellationToken);
        var companiesWithCostSheets = await _db.cost_sheets
            .AsNoTracking()
            .Where(cs => cs.company != null && companyNames.Contains(cs.company))
            .Select(cs => cs.company!)
            .Distinct()
            .ToListAsync(cancellationToken);
        var companyCostSheetSet = companiesWithCostSheets.ToHashSet();
        var contactRows = await _db.details
            .AsNoTracking()
            .Where(d => customerIds.Contains(d.cust_id))
            .OrderBy(d => d.id)
            .Select(d => new
            {
                d.id,
                d.cust_id,
                d.contact_name,
                d.contact_tel,
                d.contact_email,
                d.contact_tel_office
            })
            .ToListAsync(cancellationToken);
        var primaryContactsByCustomerId = contactRows
            .GroupBy(d => d.cust_id)
            .ToDictionary(g => g.Key, g => g.First());
        var customerIdsWithProductInfo = await _db.details
            .AsNoTracking()
            .Where(d => customerIds.Contains(d.cust_id) && _db.detail_devices.Any(dd => dd.dtl_id == d.id))
            .Select(d => d.cust_id)
            .Distinct()
            .ToListAsync(cancellationToken);
        var productInfoSet = customerIdsWithProductInfo.ToHashSet();

        return customers.Select(c =>
        {
            var businessType = "Others";
            if (c.business_type_id.HasValue && c.business_type_id.Value > 0)
            {
                businessTypesById.TryGetValue((uint)c.business_type_id.Value, out businessType);
                businessType ??= "Others";
            }

            string? saleName = null;
            if (c.sale_id.HasValue && c.sale_id.Value > 0)
            {
                usersById.TryGetValue((uint)c.sale_id.Value, out saleName);
            }

            string? telesaleName = null;
            if (c.telesale_id.HasValue && c.telesale_id.Value > 0)
            {
                usersById.TryGetValue((uint)c.telesale_id.Value, out telesaleName);
            }

            primaryContactsByCustomerId.TryGetValue(c.id, out var primaryContact);

            return new CustomerListRow
            {
                Customer = c,
                BusinessType = businessType,
                SaleName = saleName,
                TelesaleName = telesaleName,
                HasCostSheet = c.name != null && companyCostSheetSet.Contains(c.name),
                PrimaryContactName = primaryContact?.contact_name,
                PrimaryContactTel = primaryContact?.contact_tel,
                PrimaryContactEmail = primaryContact?.contact_email,
                PrimaryContactTelOffice = primaryContact?.contact_tel_office,
                HasProductLicenseInfo = productInfoSet.Contains(c.id)
            };
        }).ToList();
    }

    private Expression<Func<customer, bool>> BuildCustomerSearchPredicate(MultiTokenSearchTerm searchTerm)
    {
        Expression<Func<customer, bool>> predicate = c => false;

        foreach (var token in searchTerm.Tokens)
        {
            var text = token.Text;
            var phone = token.PhoneText;
            uint? numericId = uint.TryParse(text, out var parsedId) ? parsedId : null;

            predicate = predicate.Or(c =>
                (numericId.HasValue && c.id == numericId.Value) ||
                (c.name != null && (c.name.ToLower() == text || c.name.ToLower().StartsWith(text) || c.name.ToLower().Contains(text))) ||
                (c.address != null && (c.address.ToLower() == text || c.address.ToLower().StartsWith(text) || c.address.ToLower().Contains(text))) ||
                (c.code != null && (c.code.ToLower() == text || c.code.ToLower().StartsWith(text) || c.code.ToLower().Contains(text))) ||
                (c.phone != null && c.phone.ToLower().Contains(text)));
        }

        return predicate;
    }

    private static CustomerResponseDto MapCustomerListRow(CustomerListRow row, DateOnly today, string? matchedField = null)
    {
        var c = row.Customer;
        return new CustomerResponseDto
        {
            id = c.id,
            name = c.name ?? "Unnamed",
            address = c.address ?? "",
            capital = c.capital?.ToString() ?? "0",
            telesale_id = c.telesale_id,
            telesale = row.TelesaleName,
            sale_id = c.sale_id,
            sale = row.SaleName,
            status = c.status,
            is_active = c.is_active ?? true,
            start_dt = c.start_dt?.ToString("yyyy-MM-dd"),
            bt_type = row.BusinessType,
            renewalDays = c.start_dt.HasValue ? c.start_dt.Value.AddYears(1).DayNumber - today.DayNumber : 30,
            hasCostSheet = row.HasCostSheet,
            updatedAt = c.updated_at?.ToString("yyyy-MM-dd") ?? c.created_at?.ToString("yyyy-MM-dd") ?? "",
            phone = c.phone,
            subdistrict = c.subdistrict,
            district = c.district,
            province = c.province,
            postal_code = c.postal_code,
            primary_contact_name = row.PrimaryContactName,
            primary_contact_tel = row.PrimaryContactTel,
            primary_contact_email = row.PrimaryContactEmail,
            primary_contact_tel_office = row.PrimaryContactTelOffice,
            hasProductLicenseInfo = row.HasProductLicenseInfo,
            matchedField = matchedField
        };
    }

    private sealed class CustomerListRow
    {
        public customer Customer { get; set; } = null!;
        public string BusinessType { get; set; } = "Others";
        public string? SaleName { get; set; }
        public string? TelesaleName { get; set; }
        public bool HasCostSheet { get; set; }
        public string? PrimaryContactName { get; set; }
        public string? PrimaryContactTel { get; set; }
        public string? PrimaryContactEmail { get; set; }
        public string? PrimaryContactTelOffice { get; set; }
        public bool HasProductLicenseInfo { get; set; }
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

        DateOnly? startDate = null;
        if (!string.IsNullOrEmpty(dto.start_dt))
        {
            if (DateOnly.TryParse(dto.start_dt, out var parsedDate))
            {
                startDate = parsedDate;
            }
        }
        else
        {
            startDate = DateOnly.FromDateTime(DateTime.Today);
        }

        var c = new customer
        {
            name = dto.name,
            address = dto.address,
            phone = dto.phone,
            capital = double.TryParse(cleanCapital, out var cap) ? cap : 0,
            business_type_id = btId,
            status = "Not Called",
            create_type = "Key",
            is_active = true,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow,
            start_dt = startDate,
            owner_id = (int?)userId.Value,
            subdistrict = dto.subdistrict,
            district = dto.district,
            province = dto.province,
            postal_code = dto.postal_code
        };

        if (role == AppRoles.Sale)
        {
            c.sale_id = (int?)userId.Value;
            c.is_assign_sale = true;
        }
        else if (role == AppRoles.TeleSale)
        {
            c.telesale_id = (int?)userId.Value;
            c.is_assign_telesale = true;
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
        if (dto.status != null)
        {
            if (!StatusPolicy.IsValidCustomerStatus(dto.status))
            {
                return BadRequest(new { message = StatusPolicy.GetInvalidStatusMessage("Customer", dto.status, StatusPolicy.CustomerStatuses) });
            }
            c.status = dto.status;
        }
        if (dto.subdistrict != null) c.subdistrict = dto.subdistrict;
        if (dto.district != null) c.district = dto.district;
        if (dto.province != null) c.province = dto.province;
        if (dto.postal_code != null) c.postal_code = dto.postal_code;
        
        if (dto.bt_type != null)
        {
            var bt = await _db.business_types.FirstOrDefaultAsync(b => b.type == dto.bt_type, cancellationToken);
            if (bt != null) c.business_type_id = (int)bt.id;
        }

        if (dto.start_dt != null)
        {
            if (string.IsNullOrEmpty(dto.start_dt))
            {
                c.start_dt = null;
            }
            else if (DateOnly.TryParse(dto.start_dt, out var parsedDate))
            {
                c.start_dt = parsedDate;
            }
        }

        c.updated_at = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await MapToResponseDto(c, cancellationToken));
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateCustomerStatus(uint id, [FromBody] CustomerStatusUpdateDto dto, CancellationToken cancellationToken)
    {
        if (!User.CanWriteCustomerWorkflow()) return Forbid();

        if (!StatusPolicy.IsValidCustomerStatus(dto.status))
        {
            return BadRequest(new { message = StatusPolicy.GetInvalidStatusMessage("Customer", dto.status, StatusPolicy.CustomerStatuses) });
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

        c.status = dto.status;
        c.updated_at = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(await MapToResponseDto(c, cancellationToken));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCustomer(uint id, CancellationToken cancellationToken)
    {
        var role = User.GetUserRole();
        if (role != AppRoles.SuperAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You do not have permission to delete customer records." });
        }

        var c = await _db.customers.FindAsync(new object[] { id }, cancellationToken);
        if (c == null)
        {
            return NotFound();
        }

        // Manual cascade delete of child entities
        var details = await _db.details.Where(x => x.cust_id == id).ToListAsync(cancellationToken);
        foreach (var dtl in details)
        {
            var devices = await _db.detail_devices.Where(x => x.dtl_id == dtl.id).ToListAsync(cancellationToken);
            _db.detail_devices.RemoveRange(devices);

            var projects = await _db.detail_pjs.Where(x => x.dtl_id == dtl.id).ToListAsync(cancellationToken);
            _db.detail_pjs.RemoveRange(projects);
        }
        _db.details.RemoveRange(details);

        _db.customers.Remove(c);
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(true);
    }

    [HttpPut("{id}/assign")]
    public async Task<IActionResult> AssignCustomer(uint id, [FromBody] CustomerAssignDto dto, CancellationToken cancellationToken)
    {
        return BadRequest(new { message = "Assignment is no longer supported." });
    }

    [HttpPut("{id}/book")]
    public async Task<IActionResult> BookCustomer(uint id, CancellationToken cancellationToken)
    {
        return BadRequest(new { message = "Booking is no longer supported." });
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
                point = 0,
                total_point = 0
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
            point = 0,
            total_point = 0
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
            point = 0,
            total_point = 0
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
            point = 0,
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
            point = 0,
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
            point = 0,
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
        dd.point = 0;
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
            point = 0,
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
                point = 0,
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
            point = 0,
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
            point = 0,
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
        dp.point = 0;
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
            point = 0,
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
                    points = 0,
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
                    points = 0,
                    status = d.progress_status ?? "New",
                    type = "Device License"
                });
            }
        }

        var agentPerformance = new List<object>();

        return Ok(new
        {
            projectLedger,
            agentPerformance
        });
    }

    private async Task<object> CalculateMetricsAsync(IQueryable<customer> query, DateOnly today, CancellationToken cancellationToken)
    {
        var allCustomers = await query.AsNoTracking().Select(c => new {
            c.id,
            c.phone,
            c.address,
            c.business_type_id,
            c.start_dt,
            primary_contact_name = _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_name).FirstOrDefault(),
            primary_contact_tel = _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_tel).FirstOrDefault(),
            primary_contact_email = _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_email).FirstOrDefault(),
            primary_contact_tel_office = _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_tel_office).FirstOrDefault()
        }).ToListAsync(cancellationToken);

        var total = allCustomers.Count;
        var completeCount = 0;
        var incompleteCount = 0;
        var nearRenewal = 0;

        foreach (var c in allCustomers)
        {
            bool hasContact = !string.IsNullOrWhiteSpace(c.primary_contact_name);
            bool hasEmail = !string.IsNullOrWhiteSpace(c.primary_contact_email);
            bool hasPhone = !string.IsNullOrWhiteSpace(c.primary_contact_tel);
            bool hasOfficePhone = !string.IsNullOrWhiteSpace(c.primary_contact_tel_office);

            bool isComplete = hasContact && hasEmail && hasPhone && hasOfficePhone;
            if (isComplete) completeCount++;
            else incompleteCount++;

            if (c.start_dt.HasValue)
            {
                var renewalDate = c.start_dt.Value.AddYears(1);
                var daysToRenewal = renewalDate.DayNumber - today.DayNumber;
                if (daysToRenewal >= 0 && daysToRenewal <= 30)
                {
                    nearRenewal++;
                }
            }
        }

        return new
        {
            total,
            complete = completeCount,
            incomplete = incompleteCount,
            nearRenewal
        };
    }

    private IQueryable<customer> ApplyCompletenessFilters(
        IQueryable<customer> query,
        string? completeness,
        string? missingField)
    {
        if (string.IsNullOrEmpty(completeness) && string.IsNullOrEmpty(missingField))
        {
            return query;
        }

        if (!string.IsNullOrEmpty(completeness))
        {
            if (completeness.Equals("complete", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c =>
                    // hasContactName
                    (_db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_name).FirstOrDefault() != null && _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_name).FirstOrDefault() != "") &&
                    // hasContactEmail
                    (_db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_email).FirstOrDefault() != null && _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_email).FirstOrDefault() != "") &&
                    // hasContactTel
                    (_db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_tel).FirstOrDefault() != null && _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_tel).FirstOrDefault() != "") &&
                    // hasContactTelOffice
                    (_db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_tel_office).FirstOrDefault() != null && _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_tel_office).FirstOrDefault() != "")
                );
            }
            else if (completeness.Equals("incomplete", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c =>
                    // !hasContactName
                    (_db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_name).FirstOrDefault() == null || _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_name).FirstOrDefault() == "") ||
                    // !hasContactEmail
                    (_db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_email).FirstOrDefault() == null || _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_email).FirstOrDefault() == "") ||
                    // !hasContactTel
                    (_db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_tel).FirstOrDefault() == null || _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_tel).FirstOrDefault() == "") ||
                    // !hasContactTelOffice
                    (_db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_tel_office).FirstOrDefault() == null || _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_tel_office).FirstOrDefault() == "")
                );
            }
        }

        if (!string.IsNullOrEmpty(missingField))
        {
            if (missingField.Equals("noPhone", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c =>
                    _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_tel).FirstOrDefault() == null || _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_tel).FirstOrDefault() == ""
                );
            }
            else if (missingField.Equals("noContact", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c =>
                    _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_name).FirstOrDefault() == null || _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_name).FirstOrDefault() == ""
                );
            }
            else if (missingField.Equals("noEmail", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c =>
                    _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_email).FirstOrDefault() == null || _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_email).FirstOrDefault() == ""
                );
            }
            else if (missingField.Equals("noOfficePhone", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(c =>
                    _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_tel_office).FirstOrDefault() == null || _db.details.Where(d => d.cust_id == c.id).OrderBy(d => d.id).Select(d => d.contact_tel_office).FirstOrDefault() == ""
                );
            }
        }

        return query;
    }

    private async Task RecalculatePoints(uint dtlId, CancellationToken cancellationToken)
    {
        var contact = await _db.details.FindAsync(new object[] { dtlId }, cancellationToken);
        if (contact != null)
        {
            contact.point = 0;
            contact.total_point = 0;
            
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

        var primaryContact = await _db.details
            .AsNoTracking()
            .Where(d => d.cust_id == c.id)
            .OrderBy(d => d.id)
            .FirstOrDefaultAsync(cancellationToken);

        bool hasProductLicenseInfo = false;
        if (primaryContact != null)
        {
            hasProductLicenseInfo = await _db.detail_devices
                .AsNoTracking()
                .AnyAsync(dd => dd.dtl_id == primaryContact.id, cancellationToken);
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
            updatedAt = c.updated_at?.ToString("yyyy-MM-dd") ?? c.created_at?.ToString("yyyy-MM-dd") ?? "",
            phone = c.phone,
            subdistrict = c.subdistrict,
            district = c.district,
            province = c.province,
            postal_code = c.postal_code,
            primary_contact_name = primaryContact?.contact_name,
            primary_contact_tel = primaryContact?.contact_tel,
            primary_contact_email = primaryContact?.contact_email,
            primary_contact_tel_office = primaryContact?.contact_tel_office,
            hasProductLicenseInfo = hasProductLicenseInfo
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

    [StringLength(255)]
    public string? subdistrict { get; set; }

    [StringLength(255)]
    public string? district { get; set; }

    [StringLength(255)]
    public string? province { get; set; }

    [StringLength(10)]
    public string? postal_code { get; set; }

    public string? start_dt { get; set; }
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

    [StringLength(255)]
    public string? subdistrict { get; set; }

    [StringLength(255)]
    public string? district { get; set; }

    [StringLength(255)]
    public string? province { get; set; }

    [StringLength(10)]
    public string? postal_code { get; set; }

    public string? start_dt { get; set; }
}

public class CustomerStatusUpdateDto
{
    [Required]
    [StringLength(50)]
    public string status { get; set; } = null!;
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
