using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;
using Telesale.Api.Helpers;

namespace Telesale.Api.Services;

public sealed class CustomerContextService : ICustomerContextService
{
    private const int CandidateQueryLimit = 25;
    private const int ReturnedCandidateLimit = 5;
    private const int ContactLimitPerCustomer = 3;
    private readonly TelesaleDbContext _db;

    public CustomerContextService(TelesaleDbContext db)
    {
        _db = db;
    }

    public async Task<CustomerContextResult> GetCustomerContextAsync(
        string message,
        uint? contextCustomerId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var scopedCustomers = _db.customers.ApplyCustomerScope(user, _db).AsNoTracking();

        if (contextCustomerId.HasValue)
        {
            var selected = await scopedCustomers
                .Where(c => c.id == contextCustomerId.Value)
                .Take(1)
                .Select(c => new CustomerContextRow
                {
                    Id = c.id,
                    CompanyName = c.name,
                    Phone = c.phone,
                    Address = c.address,
                    Status = c.status,
                    BusinessTypeId = c.business_type_id,
                    UpdatedAt = c.updated_at ?? c.created_at
                })
                .ToListAsync(cancellationToken);

            return new CustomerContextResult(await HydrateRowsAsync(selected, cancellationToken));
        }

        var keyword = ExtractCompanyKeyword(message);
        var searchTerm = CustomerSearch.NormalizeMultiToken(keyword);
        if (searchTerm == null)
        {
            return new CustomerContextResult(Array.Empty<CustomerContextCustomer>());
        }

        var predicate = BuildCustomerSearchPredicate(searchTerm);
        var candidateRows = await scopedCustomers
            .Where(predicate)
            .OrderBy(c => c.id)
            .Take(CandidateQueryLimit)
            .Select(c => new CustomerContextRow
            {
                Id = c.id,
                CompanyName = c.name,
                Phone = c.phone,
                Address = c.address,
                Status = c.status,
                BusinessTypeId = c.business_type_id,
                UpdatedAt = c.updated_at ?? c.created_at
            })
            .ToListAsync(cancellationToken);

        var hydrated = await HydrateRowsAsync(candidateRows, cancellationToken);
        var ranked = hydrated
            .Select(customer =>
            {
                var document = new CustomerSearchDocument(
                    new Models.customer
                    {
                        id = customer.Id,
                        name = customer.CompanyName,
                        phone = customer.Phone,
                        address = customer.Address,
                        status = customer.Status ?? string.Empty,
                        create_type = "Key"
                    },
                    customer.BusinessType,
                    SaleName: null,
                    TelesaleName: null,
                    customer.Contacts.Select(c => new ContactSearchDocument(c.Name, c.Phone, c.Email)));

                return new
                {
                    Customer = customer,
                    Match = CustomerSearch.RankDocument(document, searchTerm)
                };
            })
            .Where(x => x.Match != null)
            .OrderBy(x => x.Match!.Rank)
            .ThenBy(x => x.Customer.Id)
            .Take(ReturnedCandidateLimit)
            .Select(x => x.Customer)
            .ToList();

        return new CustomerContextResult(ranked);
    }

    internal static string ExtractCompanyKeyword(string message)
    {
        var cleaned = CleanMessage(message);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return string.Empty;
        }

        var match = Regex.Match(cleaned, @"(?:บริษัท|company|ลูกค้า)\s+(.+)", RegexOptions.IgnoreCase);
        return match.Success ? CleanMessage(match.Groups[1].Value) : cleaned;
    }

    private static string CleanMessage(string value)
    {
        var cleaned = Regex.Replace(value.Trim(), @"[?!.:,;""'()\[\]{}]+", " ");
        return Regex.Replace(cleaned, @"\s+", " ").Trim();
    }

    private static System.Linq.Expressions.Expression<Func<Models.customer, bool>> BuildCustomerSearchPredicate(MultiTokenSearchTerm searchTerm)
    {
        System.Linq.Expressions.Expression<Func<Models.customer, bool>> predicate = c => false;

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
                (c.phone != null && c.phone.Replace(" ", "").Replace("-", "").ToLower().Contains(phone)));
        }

        return predicate;
    }

    private async Task<IReadOnlyList<CustomerContextCustomer>> HydrateRowsAsync(
        IReadOnlyList<CustomerContextRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return Array.Empty<CustomerContextCustomer>();
        }

        var customerIds = rows.Select(r => r.Id).ToList();
        var businessTypeIds = rows
            .Where(r => r.BusinessTypeId.HasValue && r.BusinessTypeId.Value > 0)
            .Select(r => (uint)r.BusinessTypeId!.Value)
            .Distinct()
            .ToList();

        var businessTypes = await _db.business_types
            .AsNoTracking()
            .Where(bt => businessTypeIds.Contains(bt.id))
            .ToDictionaryAsync(bt => bt.id, bt => bt.type, cancellationToken);

        var contactRows = await _db.details
            .AsNoTracking()
            .Where(d => customerIds.Contains(d.cust_id))
            .OrderBy(d => d.id)
            .Select(d => new
            {
                d.cust_id,
                d.contact_name,
                d.contact_tel,
                d.contact_email
            })
            .ToListAsync(cancellationToken);

        var contactsByCustomerId = contactRows
            .GroupBy(d => d.cust_id)
            .ToDictionary(
                g => g.Key,
                g => g.Take(ContactLimitPerCustomer)
                    .Select(d => new CustomerContextContact(d.contact_name, d.contact_tel, d.contact_email))
                    .ToList());

        return rows.Select(row =>
        {
            var businessType = default(string);
            if (row.BusinessTypeId.HasValue && row.BusinessTypeId.Value > 0)
            {
                businessTypes.TryGetValue((uint)row.BusinessTypeId.Value, out businessType);
            }

            contactsByCustomerId.TryGetValue(row.Id, out var contacts);
            contacts ??= new List<CustomerContextContact>();

            return new CustomerContextCustomer(
                row.Id,
                string.IsNullOrWhiteSpace(row.CompanyName) ? "Unnamed" : row.CompanyName!,
                EmptyToNull(row.Phone),
                EmptyToNull(row.Address),
                EmptyToNull(row.Status),
                EmptyToNull(businessType),
                row.UpdatedAt,
                contacts,
                BuildMissingFields(row, businessType, contacts));
        }).ToList();
    }

    private static IReadOnlyList<string> BuildMissingFields(
        CustomerContextRow row,
        string? businessType,
        IReadOnlyList<CustomerContextContact> contacts)
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(row.Phone)) missing.Add("phone");
        if (string.IsNullOrWhiteSpace(row.Address)) missing.Add("address");
        if (string.IsNullOrWhiteSpace(businessType)) missing.Add("business type");
        if (!contacts.Any(c => !string.IsNullOrWhiteSpace(c.Name))) missing.Add("contact name");
        if (!contacts.Any(c => !string.IsNullOrWhiteSpace(c.Phone))) missing.Add("contact phone");
        if (!contacts.Any(c => !string.IsNullOrWhiteSpace(c.Email))) missing.Add("contact email");
        return missing;
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed class CustomerContextRow
    {
        public uint Id { get; init; }
        public string? CompanyName { get; init; }
        public string? Phone { get; init; }
        public string? Address { get; init; }
        public string? Status { get; init; }
        public int? BusinessTypeId { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }
}
