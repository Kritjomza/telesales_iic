using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Telesale.Api.Controllers;
using Telesale.Api.Data;
using Telesale.Api.Helpers;
using Telesale.Api.Models;
using Telesale.Api.Services;
using Xunit;

namespace Telesale.Api.Tests;

public class CustomerImportCommitTests
{
    private sealed class NoopAiExtractionService : IAiExtractionService
    {
        public Task<ColumnMappingResult> SuggestMappingsAsync(List<string> columns) => Task.FromResult(new ColumnMappingResult());

        public Task<ExtractionResult> ExtractStructuredDataAsync(string unstructuredText) => Task.FromResult(new ExtractionResult());

        public Task<string> ExplainIssueAsync(
            string issueType,
            string fieldName,
            string fieldValue,
            string issueDetails,
            string? matchedCustomerDetails) => Task.FromResult(string.Empty);
    }

    private sealed class NoopImportAiExtractionService : IImportAiExtractionService
    {
        public Task<ExtractionResult> ExtractStructuredDataAsync(string unstructuredText) => Task.FromResult(new ExtractionResult());

        public Task<string> ExplainIssueAsync(
            string issueType,
            string fieldName,
            string fieldValue,
            string issueDetails,
            string? matchedCustomerDetails) => Task.FromResult(string.Empty);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Telesale.Api.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; } = "Test";
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static TelesaleDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TelesaleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new TelesaleDbContext(options);
    }

    private static ClaimsPrincipal CreateAdminUser()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "7"),
            new Claim(ClaimTypes.Role, AppRoles.Admin),
            new Claim(ClaimTypes.Name, "ImportAdmin")
        }, "TestAuth");

        return new ClaimsPrincipal(identity);
    }

    private static ImportController CreateController(TelesaleDbContext db)
    {
        var aiService = new NoopAiExtractionService();
        var normalizationService = new ImportNormalizationService();
        var duplicateService = new ImportDuplicateDetectionService(db);
        var validationService = new ImportValidationService(
            db,
            duplicateService,
            normalizationService,
            new NoopImportAiExtractionService());

        var controller = new ImportController(
            aiService,
            validationService,
            duplicateService,
            new ImportColumnMappingService(aiService),
            new ImportPolicyService(),
            db,
            NullLogger<ImportController>.Instance,
            new TestWebHostEnvironment());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = CreateAdminUser() }
        };

        return controller;
    }

    [Fact]
    public async Task CommitCustomers_UpdatesExistingCustomerMatchedByNormalizedCompanyName()
    {
        await using var db = CreateDbContext();
        db.business_types.Add(new business_type { id = 1, type = "Retail", is_active = true });
        db.customers.Add(new customer
        {
            id = 1,
            name = "Acme Co",
            address = "Old address",
            phone = "021111111",
            business_type_id = 1,
            status = "Not Called",
            create_type = "Key",
            is_active = true
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.CommitCustomers(new ManualCommitRequest
        {
            Rows = new List<ManualImportRow>
            {
                new()
                {
                    Name = "  Acme   Co  ",
                    Address = "New address",
                    Phone = "02-222-2222",
                    BusinessType = "Retail",
                    ContactName = "Jane Doe"
                }
            },
            FileName = "customers.csv"
        }, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        AssertResponseCount(ok.Value, "imported", 0);
        AssertResponseCount(ok.Value, "updated", 1);
        AssertResponseCount(ok.Value, "skipped", 0);

        var customers = await db.customers.OrderBy(customer => customer.id).ToListAsync();
        Assert.Single(customers);
        Assert.Equal("Acme Co", customers[0].name);
        Assert.Equal("New address", customers[0].address);
        Assert.Equal("022222222", customers[0].phone);

        var contact = Assert.Single(await db.details.ToListAsync());
        Assert.Equal(customers[0].id, contact.cust_id);
        Assert.Equal("Jane Doe", contact.contact_name);
    }

    [Fact]
    public async Task CommitCustomers_ImportingSameFileTwiceDoesNotIncreaseCustomerCount()
    {
        await using var db = CreateDbContext();
        db.business_types.Add(new business_type { id = 1, type = "Retail", is_active = true });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var request = new ManualCommitRequest
        {
            Rows = new List<ManualImportRow>
            {
                new()
                {
                    Name = "Acme Co",
                    Address = "First address",
                    Phone = "02-111-1111",
                    BusinessType = "Retail"
                }
            },
            FileName = "customers.csv"
        };

        var first = await controller.CommitCustomers(request, default);
        AssertResponseCount(Assert.IsType<OkObjectResult>(first).Value, "imported", 1);

        request.Rows[0].Address = "Second address";
        var second = await controller.CommitCustomers(request, default);

        var ok = Assert.IsType<OkObjectResult>(second);
        AssertResponseCount(ok.Value, "imported", 0);
        AssertResponseCount(ok.Value, "updated", 1);

        var customer = Assert.Single(await db.customers.ToListAsync());
        Assert.Equal("Second address", customer.address);
    }

    [Fact]
    public async Task CommitCustomers_ReplacesImportedFieldsButPreservesStartDateOnUpdate()
    {
        await using var db = CreateDbContext();
        db.business_types.Add(new business_type { id = 1, type = "Retail", is_active = true });
        var originalStartDate = new DateOnly(2026, 1, 15);
        db.customers.Add(new customer
        {
            id = 1,
            name = "Acme Co",
            address = "Old address",
            phone = "021111111",
            capital = 1000000,
            business_type_id = 1,
            start_dt = originalStartDate,
            status = "Not Called",
            create_type = "Key",
            is_active = true
        });
        db.details.Add(new detail
        {
            id = 1,
            cust_id = 1,
            contact_name = "Old Contact",
            contact_email = "old@example.com",
            contact_tel = "029999999",
            contact_position = "Owner",
            is_active = true
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.CommitCustomers(new ManualCommitRequest
        {
            Rows = new List<ManualImportRow>
            {
                new()
                {
                    Name = "Acme Co",
                    Address = "",
                    Phone = "",
                    Capital = null,
                    BusinessType = "Retail",
                    ContactName = "",
                    ContactEmail = "",
                    ContactTel = "",
                    ContactPosition = ""
                }
            },
            FileName = "customers.csv"
        }, default);

        var ok = Assert.IsType<OkObjectResult>(result);
        AssertResponseCount(ok.Value, "imported", 0);
        AssertResponseCount(ok.Value, "updated", 1);

        var customer = Assert.Single(await db.customers.ToListAsync());
        Assert.Equal(originalStartDate, customer.start_dt);
        Assert.Equal("Acme Co", customer.name);
        Assert.Null(customer.address);
        Assert.Null(customer.phone);
        Assert.Null(customer.capital);

        var contact = Assert.Single(await db.details.ToListAsync());
        Assert.Null(contact.contact_name);
        Assert.Null(contact.contact_email);
        Assert.Null(contact.contact_tel);
        Assert.Null(contact.contact_position);
    }

    private static void AssertResponseCount(object? response, string propertyName, int expected)
    {
        Assert.NotNull(response);
        var property = response!.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        Assert.Equal(expected, Assert.IsType<int>(property!.GetValue(response)));
    }
}
