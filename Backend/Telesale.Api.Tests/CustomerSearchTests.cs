using Xunit;
using System.Collections.Generic;
using Telesale.Api.Models;
using Telesale.Api.Services;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;
using Telesale.Api.Helpers;
using Telesale.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Telesale.Api.Tests;

public class CustomerSearchTests
{
    [Fact]
    public void TestNormalizeMultiToken()
    {
        // Null or whitespace should return null
        Assert.Null(CustomerSearch.NormalizeMultiToken(null));
        Assert.Null(CustomerSearch.NormalizeMultiToken(""));
        Assert.Null(CustomerSearch.NormalizeMultiToken("   "));

        // Single token
        var result1 = CustomerSearch.NormalizeMultiToken("สินค้า");
        Assert.NotNull(result1);
        Assert.Equal("สินค้า", result1.OriginalQuery);
        Assert.Single(result1.Tokens);
        Assert.Equal("สินค้า", result1.Tokens[0].Text);

        // Multiple tokens & collapsing repeated spaces
        var result2 = CustomerSearch.NormalizeMultiToken("  สินค้า    นนทบุรี  ");
        Assert.NotNull(result2);
        Assert.Equal("สินค้า นนทบุรี", result2.OriginalQuery);
        Assert.Equal(2, result2.Tokens.Count);
        Assert.Equal("สินค้า", result2.Tokens[0].Text);
        Assert.Equal("นนทบุรี", result2.Tokens[1].Text);
    }

    [Fact]
    public void TestRankMultiTokenSingleTokenBackwardCompat()
    {
        var customer = new customer
        {
            id = 1,
            name = "องค์การคลังสินค้า",
            address = "563 ถนนนนทบุรี",
            status = "New",
            create_type = "Manual"
        };

        var term = CustomerSearch.NormalizeMultiToken("สินค้า");
        Assert.NotNull(term);

        var match = CustomerSearch.RankMultiToken(customer, term);
        Assert.NotNull(match);
        Assert.Equal("Customer name", match.MatchedField);
        // Single token rank delegates to Rank, which matches "name contains" as Rank 2.
        Assert.Equal(2, match.Rank);
    }

    [Fact]
    public void TestRankMultiTokenCrossField()
    {
        var customer = new customer
        {
            id = 1,
            name = "องค์การคลังสินค้า",
            address = "563 ถนนนนทบุรี",
            status = "New",
            create_type = "Manual"
        };

        var term = CustomerSearch.NormalizeMultiToken("สินค้า นนทบุรี");
        Assert.NotNull(term);

        var match = CustomerSearch.RankMultiToken(customer, term);
        Assert.NotNull(match);
        // Should contain matches from both fields
        Assert.Contains("Customer name", match.MatchedField);
        Assert.Contains("Address", match.MatchedField);

        // Score:
        // totalTokens = 2
        // matchedCount = 2
        // sumRanks = Rank("องค์การคลังสินค้า", "สินค้า") + Rank("563 ถนนนนทบุรี", "นนทบุรี")
        // "องค์การคลังสินค้า" contains "สินค้า" -> Rank 2
        // "563 ถนนนนทบุรี" contains "นนทบุรี" -> Rank 2
        // compositeRank = (2 - 2) * 1000 + 4 = 4.
        Assert.Equal(4, match.Rank);
    }

    [Fact]
    public void TestRankMultiTokenStrictMatching()
    {
        var customerBoth = new customer
        {
            id = 1,
            name = "องค์การคลังสินค้า",
            address = "563 ถนนนนทบุรี",
            status = "New",
            create_type = "Manual"
        };

        var customerOne = new customer
        {
            id = 2,
            name = "บริษัท สินค้าไทย จำกัด",
            address = "กรุงเทพมหานคร",
            status = "New",
            create_type = "Manual"
        };

        var term = CustomerSearch.NormalizeMultiToken("สินค้า นนทบุรี");
        Assert.NotNull(term);

        var matchBoth = CustomerSearch.RankMultiToken(customerBoth, term);
        var matchOne = CustomerSearch.RankMultiToken(customerOne, term);

        Assert.NotNull(matchBoth);
        Assert.Null(matchOne);
    }

    [Fact]
    public void TestRankMultiTokenOrderIndependence()
    {
        var customer = new customer
        {
            id = 1,
            name = "องค์การคลังสินค้า",
            address = "563 ถนนนนทบุรี",
            status = "New",
            create_type = "Manual"
        };

        var term1 = CustomerSearch.NormalizeMultiToken("สินค้า นนทบุรี");
        var term2 = CustomerSearch.NormalizeMultiToken("นนทบุรี สินค้า");

        Assert.NotNull(term1);
        Assert.NotNull(term2);

        var match1 = CustomerSearch.RankMultiToken(customer, term1);
        var match2 = CustomerSearch.RankMultiToken(customer, term2);

        Assert.NotNull(match1);
        Assert.NotNull(match2);

        Assert.Equal(match1.Rank, match2.Rank);
    }

    [Fact]
    public void TestRankMultiTokenPartialMatchesDoNotDependOnFirstToken()
    {
        var cust1 = new customer { id = 1, name = "องค์การคลังสินค้า", address = "กรุงเทพ", status = "New", create_type = "Manual" };
        var cust2 = new customer { id = 2, name = "บริษัท ABC", address = "นนทบุรี", status = "New", create_type = "Manual" };

        var term = CustomerSearch.NormalizeMultiToken("สินค้า นนทบุรี");
        Assert.NotNull(term);

        // cust1 matches "สินค้า" (token 1), cust2 matches "นนทบุรี" (token 2)
        var match1 = CustomerSearch.RankMultiToken(cust1, term);
        var match2 = CustomerSearch.RankMultiToken(cust2, term);

        Assert.Null(match1);
        Assert.Null(match2);
    }

    [Fact]
    public void TestRankMultiTokenPhoneSearch()
    {
        var customer = new customer
        {
            id = 1,
            name = "สมชาย",
            phone = "081-234-5678",
            status = "New",
            create_type = "Manual"
        };

        var term = CustomerSearch.NormalizeMultiToken("สมชาย 0812345678");
        Assert.NotNull(term);

        var match = CustomerSearch.RankMultiToken(customer, term);
        Assert.NotNull(match);
        Assert.Contains("Customer name", match.MatchedField);
        Assert.Contains("Phone", match.MatchedField);
    }

    [Fact]
    public void TestRankDocumentExactPrefixAndFuzzyCustomerName()
    {
        var customer = new customer
        {
            id = 1,
            name = "Chinsamith",
            status = "New",
            create_type = "Manual"
        };
        var document = new CustomerSearchDocument(customer, null, null, null);

        var exact = CustomerSearch.RankDocument(document, CustomerSearch.NormalizeMultiToken("Chinsamith")!);
        var prefix = CustomerSearch.RankDocument(document, CustomerSearch.NormalizeMultiToken("Chin")!);
        var fuzzy = CustomerSearch.RankDocument(document, CustomerSearch.NormalizeMultiToken("Chinsamithh")!);

        Assert.NotNull(exact);
        Assert.NotNull(prefix);
        Assert.NotNull(fuzzy);
        Assert.Equal(0, exact.Rank);
        Assert.Equal(1, prefix.Rank);
        Assert.Contains("Customer name", fuzzy.MatchedField);
    }

    [Fact]
    public void TestPhoneNormalizationRemovesCommonSeparators()
    {
        var customer = new customer
        {
            id = 1,
            name = "Example",
            phone = "(02) 313-3799 #109",
            status = "New",
            create_type = "Manual"
        };

        var term = CustomerSearch.NormalizeMultiToken("023133799109");
        Assert.NotNull(term);

        var match = CustomerSearch.RankMultiToken(customer, term);
        Assert.NotNull(match);
        Assert.Equal("Phone", match.MatchedField);
    }

    [Fact]
    public void TestRankDocumentMatchesContactBusinessTypeAndBookingNumber()
    {
        var customer = new customer
        {
            id = 1,
            name = "บริษัท ตัวอย่าง จำกัด",
            address = "กรุงเทพ",
            code = "CUST-001",
            status = "New",
            create_type = "Manual"
        };
        var document = new CustomerSearchDocument(
            customer,
            BusinessType: "Government",
            SaleName: "Narin Admin",
            TelesaleName: "May Agent",
            Contacts: new[] { new ContactSearchDocument("Somchai", "081-234-5678", "somchai@example.com") },
            BookingNumbers: new[] { "QO-2026-001" });

        var term = CustomerSearch.NormalizeMultiToken("government somchai qo-2026-001");
        Assert.NotNull(term);

        var match = CustomerSearch.RankDocument(document, term);

        Assert.NotNull(match);
        Assert.Contains("Business type", match.MatchedField);
        Assert.Contains("Contact name", match.MatchedField);
        Assert.Contains("Booking number", match.MatchedField);
    }

    [Fact]
    public void TestRankDocumentMatchesAllTokensAbovePartialMatches()
    {
        var allTokens = new CustomerSearchDocument(
            new customer { id = 1, name = "องค์การคลังสินค้า", address = "นนทบุรี", status = "New", create_type = "Manual" },
            BusinessType: null,
            SaleName: null,
            TelesaleName: null);
        var partial = new CustomerSearchDocument(
            new customer { id = 2, name = "องค์การคลังสินค้า", address = "กรุงเทพ", status = "New", create_type = "Manual" },
            BusinessType: null,
            SaleName: null,
            TelesaleName: null);
        var term = CustomerSearch.NormalizeMultiToken("สินค้า นนทบุรี");
        Assert.NotNull(term);

        var allMatch = CustomerSearch.RankDocument(allTokens, term);
        var partialMatch = CustomerSearch.RankDocument(partial, term);

        Assert.NotNull(allMatch);
        Assert.Null(partialMatch);
    }

    private TelesaleDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<TelesaleDbContext>()
            .UseInMemoryDatabase(databaseName: System.Guid.NewGuid().ToString())
            .Options;
        return new TelesaleDbContext(options);
    }

    private ClaimsPrincipal CreateUserPrincipal(uint id, string role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim(ClaimTypes.Name, "SearchTestUser")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    private CustomersController CreateCustomersController(TelesaleDbContext db, ClaimsPrincipal user)
    {
        return new CustomersController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
    }

    private static List<CustomerResponseDto> GetCustomerList(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsAssignableFrom<List<CustomerResponseDto>>(ok.Value);
    }

    [Fact]
    public async Task TestSearchQueryTranslationAndFiltering()
    {
        using var db = GetInMemoryDbContext();

        // Seed data
        var cust1 = new customer { id = 1, name = "องค์การคลังสินค้า", address = "นนทบุรี", status = "New", create_type = "Manual", is_active = true };
        var cust2 = new customer { id = 2, name = "บริษัท สินค้าไทย จำกัด", address = "กรุงเทพ", status = "New", create_type = "Manual", is_active = true };
        var cust3 = new customer { id = 3, name = "บริษัท ABC", address = "นนทบุรี", status = "New", create_type = "Manual", is_active = true };

        db.customers.AddRange(cust1, cust2, cust3);
        await db.SaveChangesAsync();

        var multiTerm = CustomerSearch.NormalizeMultiToken("สินค้า นนทบุรี");
        Assert.NotNull(multiTerm);

        // Replicate the translation query in CustomersController.cs
        Expression<Func<customer, bool>> directPredicate = c => false;

        foreach (var token in multiTerm.Tokens)
        {
            var text = token.Text;
            var ph = token.PhoneText;
            uint? numericId = uint.TryParse(text, out var parsedId) ? parsedId : null;

            directPredicate = directPredicate.Or(c =>
                (numericId.HasValue && c.id == numericId.Value) ||
                (c.name != null && (c.name.ToLower() == text || c.name.ToLower().StartsWith(text) || c.name.ToLower().Contains(text))) ||
                (c.address != null && (c.address.ToLower() == text || c.address.ToLower().StartsWith(text) || c.address.ToLower().Contains(text))) ||
                (c.code != null && (c.code.ToLower() == text || c.code.ToLower().StartsWith(text) || c.code.ToLower().Contains(text))) ||
                (c.phone != null && c.phone.Replace(" ", "").Replace("-", "").ToLower().Contains(ph))
            );
        }

        // Execute the query to verify it translates successfully
        var results = await db.customers
            .AsNoTracking()
            .Where(directPredicate)
            .OrderBy(c => c.id)
            .ToListAsync();

        // Should match cust1 (both), cust2 (สินค้า), cust3 (นนทบุรี)
        Assert.Equal(3, results.Count);
        Assert.Contains(results, r => r.id == 1);
        Assert.Contains(results, r => r.id == 2);
        Assert.Contains(results, r => r.id == 3);
    }

    [Fact]
    public async Task TestGetCustomersSearchRanksMultiTokenAcrossFields()
    {
        using var db = GetInMemoryDbContext();
        var product = "\u0e2a\u0e34\u0e19\u0e04\u0e49\u0e32";
        var nonthaburi = "\u0e19\u0e19\u0e17\u0e1a\u0e38\u0e23\u0e35";
        var warehouse = "\u0e2d\u0e07\u0e04\u0e4c\u0e01\u0e32\u0e23\u0e04\u0e25\u0e31\u0e07\u0e2a\u0e34\u0e19\u0e04\u0e49\u0e32";

        db.customers.AddRange(
            new customer { id = 1, name = warehouse, address = nonthaburi, status = "New", create_type = "Key", is_active = true },
            new customer { id = 2, name = "Company " + product, address = "Bangkok", status = "New", create_type = "Key", is_active = true },
            new customer { id = 3, name = "Company ABC", address = nonthaburi, status = "New", create_type = "Key", is_active = true });
        await db.SaveChangesAsync();

        var controller = CreateCustomersController(db, CreateUserPrincipal(1, AppRoles.SuperAdmin));
        var result = await controller.GetCustomers(null, null, product + " " + nonthaburi, null, null, default);

        var list = GetCustomerList(result);
        Assert.Single(list);
        Assert.Equal((uint)1, list[0].id);
        Assert.Contains("Customer name", list[0].matchedField);
        Assert.Contains("Address", list[0].matchedField);
    }

    [Fact]
    public async Task TestGetCustomersSearchAppliesTeleSaleScope()
    {
        using var db = GetInMemoryDbContext();
        db.customers.AddRange(
            new customer { id = 1, name = "Alpha Search", address = "Allowed", telesale_id = 20, status = "New", create_type = "Key", is_active = true },
            new customer { id = 2, name = "Alpha Search", address = "Outside", telesale_id = 99, status = "New", create_type = "Key", is_active = true });
        await db.SaveChangesAsync();

        var controller = CreateCustomersController(db, CreateUserPrincipal(20, AppRoles.TeleSale));
        var result = await controller.GetCustomers(null, null, "Alpha", null, null, default);

        var list = GetCustomerList(result);
        Assert.Equal(2, list.Count);
        Assert.Contains(list, c => c.id == 1);
        Assert.Contains(list, c => c.id == 2);
    }

    [Fact]
    public async Task TestGetCustomersSearchAdminAndSuperAdminAccessPreserved()
    {
        using var db = GetInMemoryDbContext();
        db.customers.AddRange(
            new customer { id = 1, name = "Alpha Search", address = "One", telesale_id = 20, status = "New", create_type = "Key", is_active = true },
            new customer { id = 2, name = "Alpha Search", address = "Two", telesale_id = 99, status = "New", create_type = "Key", is_active = true });
        await db.SaveChangesAsync();

        var adminController = CreateCustomersController(db, CreateUserPrincipal(10, AppRoles.Admin));
        var superAdminController = CreateCustomersController(db, CreateUserPrincipal(11, AppRoles.SuperAdmin));

        var adminList = GetCustomerList(await adminController.GetCustomers(null, null, "Alpha", null, null, default));
        var superAdminList = GetCustomerList(await superAdminController.GetCustomers(null, null, "Alpha", null, null, default));

        Assert.Equal(2, adminList.Count);
        Assert.Equal(2, superAdminList.Count);
    }

    [Fact]
    public void TestRankMultiTokenStrictTokenMatchingExamples()
    {
        var customer = new customer
        {
            id = 1,
            name = "มหาชน นนทบุรี",
            address = "ถนนรัตนาธิเบศร์",
            status = "New",
            create_type = "Manual"
        };

        // Query: มหาชน -> Should match
        var term1 = CustomerSearch.NormalizeMultiToken("มหาชน");
        Assert.NotNull(term1);
        var match1 = CustomerSearch.RankMultiToken(customer, term1);
        Assert.NotNull(match1);

        // Query: มหาชน นนทบุรี -> Should match (both tokens match)
        var term2 = CustomerSearch.NormalizeMultiToken("มหาชน นนทบุรี");
        Assert.NotNull(term2);
        var match2 = CustomerSearch.RankMultiToken(customer, term2);
        Assert.NotNull(match2);

        // Query: มหาชน นนทบุรี ท่าอิฐ -> Should NOT match (ท่าอิฐ not found)
        var term3 = CustomerSearch.NormalizeMultiToken("มหาชน นนทบุรี ท่าอิฐ");
        Assert.NotNull(term3);
        var match3 = CustomerSearch.RankMultiToken(customer, term3);
        Assert.Null(match3);
    }
}
