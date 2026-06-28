using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;
using Telesale.Api.Helpers;

namespace Telesale.Api.Services;

public sealed class CustomerContextService : ICustomerContextService
{
    private const int ReturnedCandidateLimit = 5;
    private const int BroadCandidateLimit = 250;
    private const int ContactLimitPerCustomer = 3;
    private const int NearExpiryDays = 30;
    private readonly TelesaleDbContext _db;

    public CustomerContextService(TelesaleDbContext db)
    {
        _db = db;
    }

    public async Task<CustomerContextResult> GetCustomerContextAsync(
        CustomerContextRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var scopedCustomers = _db.customers.ApplyCustomerScope(user, _db).AsNoTracking();

        if (request.ToolAction == AiChatToolAction.GetNearExpiryCustomers)
        {
            return await GetGlobalNearExpiryAsync(scopedCustomers, request.Limit, request.SortBy, cancellationToken);
        }

        if (request.ContextCustomerId.HasValue)
        {
            var selected = await scopedCustomers
                .Where(c => c.id == request.ContextCustomerId.Value)
                .Take(1)
                .Select(c => new CustomerContextRow
                {
                    Id = c.id,
                    CompanyName = c.name,
                    Phone = c.phone,
                    Address = c.address,
                    Status = c.status,
                    BusinessTypeId = c.business_type_id,
                    StartDate = c.start_dt,
                    UpdatedAt = c.updated_at ?? c.created_at
                })
                .ToListAsync(cancellationToken);

            return new CustomerContextResult(await HydrateRowsAsync(selected, cancellationToken));
        }

        var keyword = request.CompanyKeyword;
        if (string.IsNullOrWhiteSpace(keyword))
        {
            keyword = ExtractCompanyKeyword(request.Message);
        }

        var tokens = BuildCompanyTokens(keyword);
        if (tokens.Count == 0)
        {
            return new CustomerContextResult(Array.Empty<CustomerContextCustomer>());
        }

        var candidateRows = await LoadCandidateRowsAsync(scopedCustomers, tokens, cancellationToken);
        var hydrated = await HydrateRowsAsync(candidateRows, cancellationToken);
        var ranked = hydrated
            .Select(customer => new
            {
                Customer = customer,
                Rank = RankCompany(customer.CompanyName, tokens, keyword)
            })
            .Where(x => x.Rank.HasValue)
            .OrderBy(x => x.Rank!.Value)
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

        var match = Regex.Match(
            cleaned,
            @"(?:บริษัท|บจก|จำกัด|company|customer|ลูกค้า)\s+(.+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return match.Success ? CleanMessage(match.Groups[1].Value) : cleaned;
    }

    internal static string NormalizeCompanyText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        normalized = Regex.Replace(normalized, @"[?!.:,;""'()\[\]{}]+", " ");
        normalized = Regex.Replace(normalized, @"\b(co|company|corp|corporation|limited|ltd)\b\.?", " ");
        normalized = Regex.Replace(normalized, @"บริษํท|บริษัํท|บริษท|บริษัท|บจก\.?|จำกัด|หจก\.?|ห้างหุ้นส่วนจำกัด", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized;
    }

    private async Task<CustomerContextResult> GetGlobalNearExpiryAsync(
        IQueryable<Models.customer> scopedCustomers,
        int? requestedLimit,
        AiChatSortBy? sortBy,
        CancellationToken cancellationToken)
    {
        var rows = await scopedCustomers
            .Where(c => c.start_dt.HasValue)
            .Select(c => new CustomerContextRow
            {
                Id = c.id,
                CompanyName = c.name,
                Phone = c.phone,
                Address = c.address,
                Status = c.status,
                BusinessTypeId = c.business_type_id,
                StartDate = c.start_dt,
                UpdatedAt = c.updated_at ?? c.created_at
            })
            .ToListAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var limit = Math.Clamp(requestedLimit ?? ReturnedCandidateLimit, 1, 20);
        var withRenewalDays = rows
            .Select(row => row with { RenewalDays = CalculateRenewalDays(row.StartDate, today) })
            .ToList();
        var nearExpiry = SortExpiryRows(
                withRenewalDays.Where(row => row.RenewalDays.HasValue && row.RenewalDays.Value <= NearExpiryDays),
                sortBy)
            .Take(limit)
            .ToList();
        var usedNearestUpcomingFallback = false;

        if (nearExpiry.Count == 0)
        {
            usedNearestUpcomingFallback = true;
            nearExpiry = SortExpiryRows(
                    withRenewalDays.Where(row => row.RenewalDays.HasValue),
                    sortBy)
                .Take(limit)
                .ToList();
        }

        return new CustomerContextResult(
            await HydrateRowsAsync(nearExpiry, cancellationToken),
            IsGlobalNearExpiry: true,
            ExpiryFieldSupported: true,
            UsedNearestUpcomingFallback: usedNearestUpcomingFallback,
            NearExpiryWindowDays: NearExpiryDays);
    }

    private static IOrderedEnumerable<CustomerContextRow> SortExpiryRows(
        IEnumerable<CustomerContextRow> rows,
        AiChatSortBy? sortBy)
    {
        return sortBy == AiChatSortBy.ExpiryDate
            ? rows.OrderBy(row => row.StartDate?.AddYears(1)).ThenBy(row => row.Id)
            : rows.OrderBy(row => row.RenewalDays).ThenBy(row => row.Id);
    }

    private async Task<IReadOnlyList<CustomerContextRow>> LoadCandidateRowsAsync(
        IQueryable<Models.customer> scopedCustomers,
        IReadOnlyList<string> tokens,
        CancellationToken cancellationToken)
    {
        var firstToken = tokens[0];
        var query = scopedCustomers.Where(c =>
            (c.name != null && c.name.ToLower().Contains(firstToken)) ||
            (c.code != null && c.code.ToLower().Contains(firstToken)) ||
            (c.phone != null && c.phone.Replace(" ", "").Replace("-", "").Contains(firstToken)));

        var rows = await SelectRows(query)
            .Take(BroadCandidateLimit)
            .ToListAsync(cancellationToken);

        if (rows.Count > 0)
        {
            return rows;
        }

        return await SelectRows(scopedCustomers)
            .Take(BroadCandidateLimit)
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<CustomerContextRow> SelectRows(IQueryable<Models.customer> query)
    {
        return query
            .OrderBy(c => c.id)
            .Select(c => new CustomerContextRow
            {
                Id = c.id,
                CompanyName = c.name,
                Phone = c.phone,
                Address = c.address,
                Status = c.status,
                BusinessTypeId = c.business_type_id,
                StartDate = c.start_dt,
                UpdatedAt = c.updated_at ?? c.created_at
            });
    }

    private static IReadOnlyList<string> BuildCompanyTokens(string? value)
    {
        var normalized = NormalizeCompanyText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return Array.Empty<string>();
        }

        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ขอ", "แล้ว", "ล่ะ", "ข้อมูล", "ที่ยังขาด", "ยังขาด", "เบอร์", "อีเมล", "ล่าสุด", "ลูกค้า", "หมดอายุ", "ใกล้หมดอายุ", "company", "customer", "info", "phone", "email", "profile"
        };

        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length > 1 && !stopWords.Contains(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int? RankCompany(string companyName, IReadOnlyList<string> tokens, string? originalKeyword)
    {
        var normalizedName = NormalizeCompanyText(companyName);
        var normalizedKeyword = NormalizeCompanyText(originalKeyword);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(normalizedKeyword) && normalizedName == normalizedKeyword)
        {
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(normalizedKeyword) && normalizedName.Contains(normalizedKeyword))
        {
            return 1;
        }

        if (tokens.All(normalizedName.Contains))
        {
            return 2;
        }

        var nameParts = normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.All(token => nameParts.Any(part => IsMinorTypoMatch(part, token) || part.Contains(token) || token.Contains(part))))
        {
            return 3;
        }

        var matchedTokens = tokens.Count(token =>
            normalizedName.Contains(token) ||
            nameParts.Any(part => IsMinorTypoMatch(part, token)));

        return matchedTokens > 0 ? 10 + (tokens.Count - matchedTokens) : null;
    }

    private static bool IsMinorTypoMatch(string value, string term)
    {
        if (value.Length < 3 || term.Length < 3)
        {
            return false;
        }

        if (Math.Abs(value.Length - term.Length) > 2)
        {
            return false;
        }

        return LevenshteinDistance(value, term) <= 2;
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    private async Task<IReadOnlyList<CustomerContextCustomer>> HydrateRowsAsync(
        IReadOnlyList<CustomerContextRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return Array.Empty<CustomerContextCustomer>();
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
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

            var renewalDays = row.RenewalDays ?? CalculateRenewalDays(row.StartDate, today);
            var expiryDate = row.StartDate?.AddYears(1);

            return new CustomerContextCustomer(
                row.Id,
                string.IsNullOrWhiteSpace(row.CompanyName) ? "Unnamed" : row.CompanyName!,
                EmptyToNull(row.Phone),
                EmptyToNull(row.Address),
                EmptyToNull(row.Status),
                EmptyToNull(businessType),
                row.UpdatedAt,
                expiryDate,
                renewalDays,
                contacts,
                BuildMissingFields(row, businessType, contacts));
        }).ToList();
    }

    private static int? CalculateRenewalDays(DateOnly? startDate, DateOnly today)
    {
        return startDate.HasValue ? startDate.Value.AddYears(1).DayNumber - today.DayNumber : null;
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
        if (!row.StartDate.HasValue) missing.Add("expiry date");
        if (!contacts.Any(c => !string.IsNullOrWhiteSpace(c.Name))) missing.Add("contact name");
        if (!contacts.Any(c => !string.IsNullOrWhiteSpace(c.Phone))) missing.Add("contact phone");
        if (!contacts.Any(c => !string.IsNullOrWhiteSpace(c.Email))) missing.Add("contact email");
        return missing;
    }

    private static string CleanMessage(string value)
    {
        var cleaned = Regex.Replace(value.Trim(), @"[?!.:,;""'()\[\]{}]+", " ");
        return Regex.Replace(cleaned, @"\s+", " ").Trim();
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record CustomerContextRow
    {
        public uint Id { get; init; }
        public string? CompanyName { get; init; }
        public string? Phone { get; init; }
        public string? Address { get; init; }
        public string? Status { get; init; }
        public int? BusinessTypeId { get; init; }
        public DateOnly? StartDate { get; init; }
        public int? RenewalDays { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }
}
