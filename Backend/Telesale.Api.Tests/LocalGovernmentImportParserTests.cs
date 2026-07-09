using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Telesale.Api.Controllers;
using Telesale.Api.Data;
using Telesale.Api.Helpers;
using Telesale.Api.Models;
using Telesale.Api.Services;

namespace Telesale.Api.Tests;

public class LocalGovernmentImportParserTests
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

    private static LocalGovernmentImportParserService CreateParser() => new();

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
            new Claim(ClaimTypes.Role, AppRoles.Admin)
        }, "TestAuth");

        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void ParsePreview_MapsThaiHeadersAndCustomerPreview()
    {
        var summary = CreateParser().ParsePreview(Headers, new[]
        {
            Row("1", "กรุงเทพมหานคร", "เทศบาลนคร", "เทศบาลนครตัวอย่าง", "02-111-2222")
        }, new HashSet<string>());

        var row = Assert.Single(summary.Rows);
        Assert.Empty(row.Errors);
        Assert.Equal("เทศบาลนครตัวอย่าง", row.OrganizationName);
        Assert.Equal("เทศบาลนครตัวอย่าง", row.NormalizedOrganizationName);
        Assert.False(row.IsDuplicate);
        Assert.NotNull(row.CustomerPreview);
        Assert.Equal("เทศบาลนครตัวอย่าง", row.CustomerPreview!.Name);
        Assert.Equal("02-111-2222", row.CustomerPreview.Phone);
        Assert.Equal("Not Called", row.CustomerPreview.Status);
        Assert.Equal("Import", row.CustomerPreview.CreateType);
        Assert.True(row.CustomerPreview.IsActive);
    }

    [Fact]
    public void ParsePreview_RequiresOrganizationName()
    {
        var summary = CreateParser().ParsePreview(Headers, new[] { Row("1", "นนทบุรี", "เทศบาล", "") }, new HashSet<string>());

        var row = Assert.Single(summary.Rows);
        Assert.Contains(row.Errors, issue => issue.Field == "ชื่อหน่วยงาน");
        Assert.Equal(1, summary.ErrorRows);
        Assert.Equal(0, summary.EstimatedCustomersToInsert);
    }

    [Fact]
    public void ParsePreview_NormalizesOrganizationNameByTrimmingAndCollapsingWhitespace()
    {
        var summary = CreateParser().ParsePreview(Headers, new[] { Row("1", "", "", "  เทศบาล   เมือง  ตัวอย่าง  ") }, new HashSet<string>());

        var row = Assert.Single(summary.Rows);
        Assert.Equal("เทศบาล เมือง ตัวอย่าง", row.NormalizedOrganizationName);
    }

    [Fact]
    public void ParsePreview_ComposesReadableAddressFromAvailableFields()
    {
        var summary = CreateParser().ParsePreview(Headers, new[]
        {
            Row("1", "เชียงใหม่", "อบต.", "อบต.ตัวอย่าง", "053-111111", schoolCount: "12", note: "ติดต่อช่วงเช้า")
        }, new HashSet<string>());

        var address = Assert.Single(summary.Rows).CustomerPreview!.Address;
        Assert.Contains("จังหวัด: เชียงใหม่", address);
        Assert.Contains("ประเภท: อบต.", address);
        Assert.Contains("จำนวนโรงเรียนในสังกัด: 12", address);
        Assert.Contains("หมายเหตุ: ติดต่อช่วงเช้า", address);
    }

    [Fact]
    public void ParsePreview_WarnsWhenAddressExceedsVarcharLimitAndTrimsSafely()
    {
        var longNote = new string('ก', 300);
        var summary = CreateParser().ParsePreview(Headers, new[]
        {
            Row("1", "เชียงใหม่", "อบต.", "อบต.ตัวอย่าง", "053-111111", note: longNote)
        }, new HashSet<string>());

        var row = Assert.Single(summary.Rows);
        Assert.True(row.CustomerPreview!.Address!.Length <= 255);
        Assert.Contains(row.Warnings, issue => issue.Field == "address");
    }

    [Fact]
    public void ParsePreview_ParsesMayorContactsByCommaAndIndex()
    {
        var summary = CreateParser().ParsePreview(Headers, new[]
        {
            Row("1", organization: "เทศบาลตัวอย่าง", mayorName: "นายหนึ่ง, นายสอง", mayorPhone: "081-111-1111, 082-222-2222")
        }, new HashSet<string>());

        var details = Assert.Single(summary.Rows).DetailPreviews;
        Assert.Equal(2, details.Count);
        Assert.Equal("นายก", details[0].ContactPosition);
        Assert.Equal("นายหนึ่ง", details[0].ContactName);
        Assert.Equal("081-111-1111", details[0].ContactTel);
        Assert.Equal("นายสอง", details[1].ContactName);
        Assert.Equal("082-222-2222", details[1].ContactTel);
    }

    [Fact]
    public void ParsePreview_ParsesDeputyMayorPipePhoneNamePairs()
    {
        var deputyPhone = "0-2382-6199 ต่อ 322 (นายสมยศ อ้นโต) | 0-2382-6199 ต่อ 323 (นางพิชญา ศิวะพิรุฬห์เทพ)";
        var summary = CreateParser().ParsePreview(Headers, new[]
        {
            Row("1", organization: "เทศบาลตัวอย่าง", deputyPhone: deputyPhone)
        }, new HashSet<string>());

        var details = Assert.Single(summary.Rows).DetailPreviews;
        Assert.Equal(2, details.Count);
        Assert.All(details, detail => Assert.Equal("รองนายก", detail.ContactPosition));
        Assert.Equal("นายสมยศ อ้นโต", details[0].ContactName);
        Assert.Equal("0-2382-6199 ต่อ 322", details[0].ContactTel);
        Assert.Equal("นางพิชญา ศิวะพิรุฬห์เทพ", details[1].ContactName);
        Assert.Equal("0-2382-6199 ต่อ 323", details[1].ContactTel);
    }

    [Fact]
    public void ParsePreview_FallsBackToDeputyMayorCommaAndIndexParser()
    {
        var summary = CreateParser().ParsePreview(Headers, new[]
        {
            Row("1", organization: "เทศบาลตัวอย่าง", deputyName: "นายรองหนึ่ง, นายรองสอง", deputyPhone: "081-111-1111, 082-222-2222")
        }, new HashSet<string>());

        var details = Assert.Single(summary.Rows).DetailPreviews;
        Assert.Equal(2, details.Count);
        Assert.Equal("นายรองหนึ่ง", details[0].ContactName);
        Assert.Equal("081-111-1111", details[0].ContactTel);
        Assert.Equal("นายรองสอง", details[1].ContactName);
        Assert.Equal("082-222-2222", details[1].ContactTel);
    }

    [Fact]
    public void ParsePreview_ParsesEducationDirectorContacts()
    {
        var summary = CreateParser().ParsePreview(Headers, new[]
        {
            Row("1", organization: "เทศบาลตัวอย่าง", educationName: "นางผอ.", educationPhone: "083-333-3333")
        }, new HashSet<string>());

        var detail = Assert.Single(Assert.Single(summary.Rows).DetailPreviews);
        Assert.Equal("ผอ.การศึกษา", detail.ContactPosition);
        Assert.Equal("นางผอ.", detail.ContactName);
        Assert.Equal("083-333-3333", detail.ContactTel);
    }

    [Fact]
    public void ParsePreview_NameWithoutPhoneCreatesContact()
    {
        var summary = CreateParser().ParsePreview(Headers, new[]
        {
            Row("1", organization: "เทศบาลตัวอย่าง", mayorName: "นายหนึ่ง")
        }, new HashSet<string>());

        var detail = Assert.Single(Assert.Single(summary.Rows).DetailPreviews);
        Assert.Equal("นายหนึ่ง", detail.ContactName);
        Assert.Null(detail.ContactTel);
    }

    [Fact]
    public void ParsePreview_PhoneWithoutNameSkipsContactAndWarns()
    {
        var summary = CreateParser().ParsePreview(Headers, new[]
        {
            Row("1", organization: "เทศบาลตัวอย่าง", mayorPhone: "081-111-1111")
        }, new HashSet<string>());

        var row = Assert.Single(summary.Rows);
        Assert.Empty(row.DetailPreviews);
        Assert.Contains(row.Warnings, issue => issue.Field == "เบอร์ (นายก)");
    }

    [Fact]
    public void ParsePreview_DetectsDuplicateAndExcludesEstimatedInsertCounts()
    {
        var summary = CreateParser().ParsePreview(
            Headers,
            new[] { Row("1", organization: "  เทศบาล   ตัวอย่าง  ", mayorName: "นายหนึ่ง") },
            new HashSet<string> { "เทศบาล ตัวอย่าง" });

        var row = Assert.Single(summary.Rows);
        Assert.True(row.IsDuplicate);
        Assert.Equal(1, summary.DuplicateRows);
        Assert.Equal(0, summary.EstimatedCustomersToInsert);
        Assert.Equal(0, summary.EstimatedDetailsToInsert);
    }

    [Fact]
    public async Task PreviewAsync_DetectsDuplicatesFromExistingCustomersWithoutWriting()
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

        var service = new LocalGovernmentImportPreviewService(db, CreateParser());
        var summary = await service.PreviewAsync(Headers, new[] { Row("1", organization: "เทศบาล   ตัวอย่าง") }, default);

        Assert.True(Assert.Single(summary.Rows).IsDuplicate);
        Assert.Equal(1, await db.customers.CountAsync());
        Assert.Empty(await db.details.ToListAsync());
    }

    [Fact]
    public void ImportController_ExposesIsolatedLocalGovernmentPreviewEndpoint()
    {
        var method = typeof(ImportController).GetMethod("PreviewLocalGovernment");

        Assert.NotNull(method);
        var post = Assert.Single(method!.GetCustomAttributes(typeof(HttpPostAttribute), false).Cast<HttpPostAttribute>());
        Assert.Equal("local-government/preview", post.Template);
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
