using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Telesale.Api.Controllers;
using Telesale.Api.Data;
using Telesale.Api.Helpers;
using Telesale.Api.Models;
using Telesale.Api.Services;

namespace Telesale.Api.Tests;

public class LocalGovernmentImportConfirmTests
{
    private static readonly string[] Headers =
    {
        "ลำดับ",
        "จังหวัด",
        "ประเภท",
        "ชื่อหน่วยงาน",
        "เบอร์หน่วยงาน",
        "ชื่อ - สกุล (นายก)",
        "เบอร์ (นายก)",
        "ชื่อ - สกุล (รองนายก)",
        "เบอร์ (รองนายก)",
        "ผอ.การศึกษา",
        "เบอร์ (ผอ.การศึกษา)",
        "จำนวนโรงเรียนในสังกัด",
        "หมายเหตุ"
    };

    private static TelesaleDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TelesaleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new TelesaleDbContext(options);
    }

    private static LocalGovernmentImportConfirmService CreateService(TelesaleDbContext db)
    {
        return new LocalGovernmentImportConfirmService(db, new LocalGovernmentImportParserService());
    }

    [Fact]
    public async Task ConfirmAsync_ValidRowsInsertCustomerAndAllExpectedDetails()
    {
        await using var db = CreateDbContext();
        var deputyPhone = "0-2382-6199 ต่อ 322 (นายสมยศ อ้นโต) | 0-2382-6199 ต่อ 323 (นางพิชญา ศิวะพิรุฬห์เทพ)";

        var result = await CreateService(db).ConfirmAsync(
            Headers,
            new[]
            {
                Row(
                    "1",
                    province: "เชียงใหม่",
                    type: "เทศบาลนคร",
                    organization: "เทศบาลนครตัวอย่าง",
                    officePhone: "053-111111",
                    mayorName: "นายกหนึ่ง",
                    mayorPhone: "081-111-1111",
                    deputyPhone: deputyPhone,
                    educationName: "ผอ.หนึ่ง",
                    educationPhone: "083-333-3333",
                    schoolCount: "12",
                    note: "ติดต่อช่วงเช้า")
            },
            userId: 7,
            default);

        Assert.Equal(1, result.InsertedCustomers);
        Assert.Equal(4, result.InsertedDetails);
        Assert.Equal(0, result.SkippedDuplicates);
        Assert.Equal(0, result.ErrorRows);

        var customer = Assert.Single(await db.customers.ToListAsync());
        Assert.Equal("เทศบาลนครตัวอย่าง", customer.name);
        Assert.Equal("053-111111", customer.phone);
        Assert.Contains("จังหวัด: เชียงใหม่", customer.address);
        Assert.Contains("ประเภท: เทศบาลนคร", customer.address);
        Assert.Contains("จำนวนโรงเรียนในสังกัด: 12", customer.address);
        Assert.Contains("หมายเหตุ: ติดต่อช่วงเช้า", customer.address);
        Assert.Equal("Not Called", customer.status);
        Assert.Equal("Import", customer.create_type);
        Assert.True(customer.is_active);
        Assert.Equal(7, customer.owner_id);
        Assert.NotNull(customer.created_at);
        Assert.NotNull(customer.updated_at);
        Assert.Null(customer.updated_user);

        var details = await db.details.OrderBy(detail => detail.id).ToListAsync();
        Assert.All(details, detail =>
        {
            Assert.Equal(customer.id, detail.cust_id);
            Assert.Equal("053-111111", detail.contact_tel_office);
            Assert.True(detail.is_active);
            Assert.NotNull(detail.created_at);
            Assert.NotNull(detail.updated_at);
        });

        Assert.Contains(details, detail =>
            detail.contact_position == "นายก"
            && detail.contact_name == "นายกหนึ่ง"
            && detail.contact_tel == "081-111-1111");
        Assert.Contains(details, detail =>
            detail.contact_position == "รองนายก"
            && detail.contact_name == "นายสมยศ อ้นโต"
            && detail.contact_tel == "0-2382-6199 ต่อ 322");
        Assert.Contains(details, detail =>
            detail.contact_position == "รองนายก"
            && detail.contact_name == "นางพิชญา ศิวะพิรุฬห์เทพ"
            && detail.contact_tel == "0-2382-6199 ต่อ 323");
        Assert.Contains(details, detail =>
            detail.contact_position == "ผอ.การศึกษา"
            && detail.contact_name == "ผอ.หนึ่ง"
            && detail.contact_tel == "083-333-3333");

        var row = Assert.Single(result.Rows);
        Assert.Equal("inserted", row.Status);
        Assert.Equal(customer.id, row.InsertedCustomerId);
        Assert.Equal(4, row.InsertedDetailCount);
    }

    [Fact]
    public async Task ConfirmAsync_DeputyFallbackCommaIndexMappingInsertsDetails()
    {
        await using var db = CreateDbContext();

        var result = await CreateService(db).ConfirmAsync(
            Headers,
            new[]
            {
                Row(
                    "1",
                    organization: "เทศบาลตัวอย่าง",
                    officePhone: "02-111-1111",
                    deputyName: "นายรองหนึ่ง, นายรองสอง",
                    deputyPhone: "081-111-1111, 082-222-2222")
            },
            userId: 7,
            default);

        Assert.Equal(1, result.InsertedCustomers);
        Assert.Equal(2, result.InsertedDetails);
        var details = await db.details.OrderBy(detail => detail.id).ToListAsync();
        Assert.Equal("นายรองหนึ่ง", details[0].contact_name);
        Assert.Equal("081-111-1111", details[0].contact_tel);
        Assert.Equal("รองนายก", details[0].contact_position);
        Assert.Equal("นายรองสอง", details[1].contact_name);
        Assert.Equal("082-222-2222", details[1].contact_tel);
        Assert.Equal("รองนายก", details[1].contact_position);
    }

    [Fact]
    public async Task ConfirmAsync_DuplicateCustomerNameSkipsCustomerAndDetails()
    {
        await using var db = CreateDbContext();
        db.customers.Add(new customer
        {
            id = 1,
            name = "เทศบาล ตัวอย่าง",
            status = "Not Called",
            create_type = "Import",
            is_active = true
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).ConfirmAsync(
            Headers,
            new[]
            {
                Row("1", organization: " เทศบาล   ตัวอย่าง ", mayorName: "นายกหนึ่ง", mayorPhone: "081-111-1111")
            },
            userId: 7,
            default);

        Assert.Equal(0, result.InsertedCustomers);
        Assert.Equal(0, result.InsertedDetails);
        Assert.Equal(1, result.SkippedDuplicates);
        Assert.Equal(1, await db.customers.CountAsync());
        Assert.Empty(await db.details.ToListAsync());
        Assert.Equal("duplicate_skipped", Assert.Single(result.Rows).Status);
    }

    [Fact]
    public async Task ConfirmAsync_DuplicateWithinSameFileSkipsLaterRowAndDetails()
    {
        await using var db = CreateDbContext();

        var result = await CreateService(db).ConfirmAsync(
            Headers,
            new[]
            {
                Row("1", organization: "เทศบาล ตัวอย่าง", mayorName: "นายกหนึ่ง"),
                Row("2", organization: " เทศบาล   ตัวอย่าง ", mayorName: "นายกสอง")
            },
            userId: 7,
            default);

        Assert.Equal(1, result.InsertedCustomers);
        Assert.Equal(1, result.InsertedDetails);
        Assert.Equal(1, result.SkippedDuplicates);
        Assert.Equal(1, await db.customers.CountAsync());
        Assert.Equal(1, await db.details.CountAsync());
        Assert.Equal("inserted", result.Rows[0].Status);
        Assert.Equal("duplicate_skipped", result.Rows[1].Status);
    }

    [Fact]
    public async Task ConfirmAsync_MissingOrganizationNameSkipsRowAsError()
    {
        await using var db = CreateDbContext();

        var result = await CreateService(db).ConfirmAsync(
            Headers,
            new[] { Row("1", organization: "", mayorName: "นายกหนึ่ง") },
            userId: 7,
            default);

        Assert.Equal(0, result.InsertedCustomers);
        Assert.Equal(0, result.InsertedDetails);
        Assert.Equal(1, result.ErrorRows);
        Assert.Empty(await db.customers.ToListAsync());
        Assert.Empty(await db.details.ToListAsync());

        var row = Assert.Single(result.Rows);
        Assert.Equal("error_skipped", row.Status);
        Assert.Contains(row.Errors, issue => issue.Field == "ชื่อหน่วยงาน");
    }

    [Fact]
    public async Task ConfirmAsync_AddressLongerThan255IsTrimmedWithWarning()
    {
        await using var db = CreateDbContext();
        var longNote = new string('ก', 300);

        var result = await CreateService(db).ConfirmAsync(
            Headers,
            new[] { Row("1", organization: "เทศบาลตัวอย่าง", note: longNote) },
            userId: 7,
            default);

        var customer = Assert.Single(await db.customers.ToListAsync());
        Assert.True(customer.address!.Length <= 255);
        Assert.Contains(result.Warnings, issue => issue.Field == "address");
    }

    [Fact]
    public async Task ConfirmAsync_PhoneWithoutContactNameWarnsAndSkipsDetail()
    {
        await using var db = CreateDbContext();

        var result = await CreateService(db).ConfirmAsync(
            Headers,
            new[] { Row("1", organization: "เทศบาลตัวอย่าง", mayorPhone: "081-111-1111") },
            userId: 7,
            default);

        Assert.Equal(1, result.InsertedCustomers);
        Assert.Equal(0, result.InsertedDetails);
        Assert.Empty(await db.details.ToListAsync());
        Assert.Contains(result.Warnings, issue => issue.Field == "เบอร์ (นายก)");
    }

    [Fact]
    public async Task ConfirmAsync_MismatchedContactNameAndPhoneCountsContinuesWithWarning()
    {
        await using var db = CreateDbContext();

        var result = await CreateService(db).ConfirmAsync(
            Headers,
            new[] { Row("1", organization: "เทศบาลตัวอย่าง", mayorName: "นายกหนึ่ง, นายกสอง", mayorPhone: "081-111-1111") },
            userId: 7,
            default);

        Assert.Equal(1, result.InsertedCustomers);
        Assert.Equal(2, result.InsertedDetails);
        Assert.Contains(result.Warnings, issue => issue.Field == "เบอร์ (นายก)");

        var details = await db.details.OrderBy(detail => detail.id).ToListAsync();
        Assert.Equal("081-111-1111", details[0].contact_tel);
        Assert.Null(details[1].contact_tel);
    }

    [Fact]
    public void ImportController_ExposesIsolatedLocalGovernmentConfirmEndpointAndKeepsPreviewEndpoint()
    {
        var confirmMethod = typeof(ImportController).GetMethod("ConfirmLocalGovernment");
        var previewMethod = typeof(ImportController).GetMethod("PreviewLocalGovernment");

        Assert.NotNull(confirmMethod);
        var confirmPost = Assert.Single(confirmMethod!.GetCustomAttributes(typeof(HttpPostAttribute), false).Cast<HttpPostAttribute>());
        Assert.Equal("local-government/confirm", confirmPost.Template);

        Assert.NotNull(previewMethod);
        var previewPost = Assert.Single(previewMethod!.GetCustomAttributes(typeof(HttpPostAttribute), false).Cast<HttpPostAttribute>());
        Assert.Equal("local-government/preview", previewPost.Template);
    }

    private static IReadOnlyList<string?> Row(
        string sequence = "",
        string province = "",
        string type = "",
        string organization = "",
        string officePhone = "",
        string mayorName = "",
        string mayorPhone = "",
        string deputyName = "",
        string deputyPhone = "",
        string educationName = "",
        string educationPhone = "",
        string schoolCount = "",
        string note = "")
    {
        return new[]
        {
            sequence,
            province,
            type,
            organization,
            officePhone,
            mayorName,
            mayorPhone,
            deputyName,
            deputyPhone,
            educationName,
            educationPhone,
            schoolCount,
            note
        };
    }
}
