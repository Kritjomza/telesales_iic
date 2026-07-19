using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Controllers;
using Telesale.Api.Data;
using Telesale.Api.Helpers;
using Telesale.Api.Models;

namespace Telesale.Api.Tests;

public class ContactUpdateTests
{
    public static TheoryData<string, string> ValidContactIdentityValues => new()
    {
        { "Jane Contact", "jane@example.com" },
        { "", "jane@example.com" },
        { "Jane Contact", "" },
        { "", "" }
    };

    [Theory]
    [MemberData(nameof(ValidContactIdentityValues))]
    public async Task UpdateContact_AllowsOptionalNameAndEmailAndPreservesOtherFields(
        string contactName,
        string contactEmail)
    {
        await using var db = CreateDbContext();
        db.customers.Add(new customer
        {
            id = 1,
            name = "Customer",
            status = "New",
            create_type = "Key",
            is_active = true
        });
        db.details.Add(new detail
        {
            id = 10,
            cust_id = 1,
            contact_name = "Old Name",
            contact_email = "old@example.com",
            contact_tel = "old mobile",
            contact_tel_office = "old office",
            contact_position = "Old Position",
            bak_point = 0,
            point = 0,
            total_point = 0,
            is_active = true
        });
        await db.SaveChangesAsync();

        var dto = new ContactUpdateDto
        {
            contact_name = contactName,
            contact_email = contactEmail,
            contact_tel = "0812345678",
            contact_tel_office = "021234567",
            contact_position = "Purchasing Manager"
        };

        Assert.Empty(Validate(dto));

        var controller = CreateController(db);
        var result = await controller.UpdateContact(10, dto, default);

        Assert.IsType<OkObjectResult>(result);
        var saved = await db.details.SingleAsync(d => d.id == 10);
        Assert.Equal(contactName, saved.contact_name);
        Assert.Equal(string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail, saved.contact_email);
        Assert.Equal("0812345678", saved.contact_tel);
        Assert.Equal("021234567", saved.contact_tel_office);
        Assert.Equal("Purchasing Manager", saved.contact_position);
    }

    [Fact]
    public void ContactUpdateDto_RejectsNonEmptyInvalidEmail()
    {
        var dto = new ContactUpdateDto { contact_email = "not-an-email" };

        var errors = Validate(dto);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(ContactUpdateDto.contact_email)));
    }

    [Fact]
    public void ContactUpdateDto_KeepsExistingLengthValidationForOtherFields()
    {
        var dto = new ContactUpdateDto { contact_position = new string('x', 256) };

        var errors = Validate(dto);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(ContactUpdateDto.contact_position)));
    }

    [Fact]
    public void ContactCreateDto_AllowsEmptyNameAndEmail()
    {
        var dto = new ContactCreateDto { contact_name = "", contact_email = "" };

        Assert.Empty(Validate(dto));
    }

    private static TelesaleDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TelesaleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TelesaleDbContext(options);
    }

    private static CustomersController CreateController(TelesaleDbContext db)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Role, AppRoles.SuperAdmin),
            new Claim(ClaimTypes.Name, "Contact Test User")
        }, "TestAuth"));

        return new CustomersController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
    }

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
