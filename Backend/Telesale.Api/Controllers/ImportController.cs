using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExcelDataReader;
using Telesale.Api.Helpers;
using Telesale.Api.Services;
using Telesale.Api.Data;
using Telesale.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Text;
using System.Globalization;

namespace Telesale.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // Increased to 10 MB for production large files
    private readonly IAiExtractionService _aiService;
    private readonly IImportValidationService _validationService;
    private readonly IImportDuplicateDetectionService _duplicateService;
    private readonly IImportColumnMappingService _columnMappingService;
    private readonly IImportPolicyService _policyService;
    private readonly TelesaleDbContext _db;
    private readonly ILogger<ImportController> _logger;

    public ImportController(
        IAiExtractionService aiService, 
        IImportValidationService validationService,
        IImportDuplicateDetectionService duplicateService,
        IImportColumnMappingService columnMappingService,
        IImportPolicyService policyService,
        TelesaleDbContext db,
        ILogger<ImportController> logger)
    {
        _aiService = aiService;
        _validationService = validationService;
        _duplicateService = duplicateService;
        _columnMappingService = columnMappingService;
        _policyService = policyService;
        _db = db;
        _logger = logger;
    }

    private string GetTempUploadsPath()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "TempImports");
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }

    private bool ValidateFileMagicBytes(Stream stream, string extension)
    {
        var buffer = new byte[4];
        int bytesRead = stream.Read(buffer, 0, 4);
        stream.Position = 0; // reset position

        if (bytesRead < 4) return false;

        if (extension == ".xlsx")
        {
            // ZIP archive magic bytes (PK..)
            return buffer[0] == 0x50 && buffer[1] == 0x4B && buffer[2] == 0x03 && buffer[3] == 0x04;
        }
        else if (extension == ".csv")
        {
            // CSV text: check that bytes are printable ASCII/UTF8 or common whitespace
            foreach (var b in buffer)
            {
                if (b < 32 && b != 10 && b != 13 && b != 9 && b != 0) return false;
            }
            return true;
        }

        return false;
    }

    private string GetMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".csv" => "text/csv",
            _ => "application/octet-stream"
        };
    }

    [HttpGet("templates/manage")]
    public IActionResult DownloadManageTemplate()
    {
        var templateDir = Path.Combine(Directory.GetCurrentDirectory(), "templates");
        var path = Path.GetFullPath(Path.Combine(templateDir, "manage-import-template.xlsx"));
        if (!path.StartsWith(templateDir, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid template path.");
        }
        if (!System.IO.File.Exists(path))
        {
            return NotFound(new { message = "Template file not found." });
        }
        return PhysicalFile(path, GetMimeType(path), "manage-import-template.xlsx");
    }

    [HttpGet("templates/profile")]
    public IActionResult DownloadProfileTemplate()
    {
        var templateDir = Path.Combine(Directory.GetCurrentDirectory(), "templates");
        var path = Path.GetFullPath(Path.Combine(templateDir, "profile-import-template.xlsx"));
        if (!path.StartsWith(templateDir, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid template path.");
        }
        if (!System.IO.File.Exists(path))
        {
            return NotFound(new { message = "Template file not found." });
        }
        return PhysicalFile(path, GetMimeType(path), "profile-import-template.xlsx");
    }

    [HttpGet("templates/antivirus-price-list")]
    public IActionResult DownloadAntivirusPriceListTemplate()
    {
        var templateDir = Path.Combine(Directory.GetCurrentDirectory(), "templates");
        var path = Path.GetFullPath(Path.Combine(templateDir, "antivirus-price-list-import-template.xlsx"));
        if (!path.StartsWith(templateDir, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid template path.");
        }
        if (!System.IO.File.Exists(path))
        {
            return NotFound(new { message = "Template file not found." });
        }
        return PhysicalFile(path, GetMimeType(path), "antivirus-price-list-import-template.xlsx");
    }

    [HttpPost("manage")]
    public async Task<IActionResult> ImportManage(IFormFile file, [FromQuery] bool commit = false, CancellationToken cancellationToken = default)
    {
        var role = User.GetUserRole();
        if (!AppRoles.IsAdminRole(role) && role != AppRoles.Manager)
        {
            return Forbid();
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded or file is empty." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { message = "File size exceeds the 10MB limit." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".csv" && extension != ".xlsx")
        {
            return BadRequest(new { message = "Unsupported file format. Please upload a .csv or .xlsx file." });
        }

        var userId = User.GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            using (var uploadStream = file.OpenReadStream())
            {
                if (!ValidateFileMagicBytes(uploadStream, extension))
                {
                    return BadRequest(new { message = "File content validation failed. The file structure does not match the extension." });
                }
            }

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var columns = new List<string>();
            var previewRows = new List<object>();
            var rowErrors = new List<object>();
            var parsedCustomers = new List<customer>();
            var parsedDetails = new List<detail>();
            int rowIndex = 1;

            using (var stream = file.OpenReadStream())
            {
                using (var reader = extension == ".csv"
                    ? ExcelReaderFactory.CreateCsvReader(stream)
                    : ExcelReaderFactory.CreateOpenXmlReader(stream))
                {
                    if (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            columns.Add(reader.GetValue(i)?.ToString()?.Trim() ?? $"Column_{i + 1}");
                        }
                    }
                    else
                    {
                        return BadRequest(new { message = "The uploaded file has no columns or data." });
                    }

                    int companyNameIdx = columns.FindIndex(c => string.Equals(c, "company_name", StringComparison.OrdinalIgnoreCase));
                    int addressIdx = columns.FindIndex(c => string.Equals(c, "address", StringComparison.OrdinalIgnoreCase));
                    int contactNameIdx = columns.FindIndex(c => string.Equals(c, "contact_name", StringComparison.OrdinalIgnoreCase));
                    int emailIdx = columns.FindIndex(c => string.Equals(c, "email", StringComparison.OrdinalIgnoreCase));
                    int phoneIdx = columns.FindIndex(c => string.Equals(c, "phone", StringComparison.OrdinalIgnoreCase));

                    if (companyNameIdx == -1 || addressIdx == -1 || contactNameIdx == -1 || emailIdx == -1 || phoneIdx == -1)
                    {
                        return BadRequest(new { message = "Missing required columns. 'company_name', 'address', 'contact_name', 'email', and 'phone' are required." });
                    }

                    var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");

                    while (reader.Read())
                    {
                        rowIndex++;
                        var companyName = companyNameIdx < reader.FieldCount ? reader.GetValue(companyNameIdx)?.ToString()?.Trim() : null;
                        var address = addressIdx < reader.FieldCount ? reader.GetValue(addressIdx)?.ToString()?.Trim() : null;
                        var contactName = contactNameIdx < reader.FieldCount ? reader.GetValue(contactNameIdx)?.ToString()?.Trim() : null;
                        var email = emailIdx < reader.FieldCount ? reader.GetValue(emailIdx)?.ToString()?.Trim() : null;
                        var phone = phoneIdx < reader.FieldCount ? reader.GetValue(phoneIdx)?.ToString()?.Trim() : null;

                        var rowIssues = new List<string>();
                        if (string.IsNullOrWhiteSpace(companyName))
                        {
                            rowIssues.Add("Company Name is required.");
                        }
                        if (string.IsNullOrWhiteSpace(address))
                        {
                            rowIssues.Add("Address is required.");
                        }
                        if (!string.IsNullOrEmpty(email) && !emailRegex.IsMatch(email))
                        {
                            rowIssues.Add($"Email '{email}' is invalid.");
                        }
                        if (!string.IsNullOrEmpty(phone))
                        {
                            var cleanPhone = new string(phone.Where(char.IsDigit).ToArray());
                            if (cleanPhone.Length < 9 || cleanPhone.Length > 10)
                            {
                                rowIssues.Add($"Phone '{phone}' is invalid. Must be 9 or 10 digits.");
                            }
                        }

                        if (rowIssues.Count > 0)
                        {
                            rowErrors.Add(new { row = rowIndex, issues = rowIssues });
                        }

                        var cust = new customer
                        {
                            name = companyName ?? string.Empty,
                            address = address ?? string.Empty,
                            phone = phone,
                            status = "New",
                            create_type = "Import",
                            is_active = true,
                            created_at = DateTime.UtcNow,
                            updated_at = DateTime.UtcNow,
                            start_dt = DateOnly.FromDateTime(DateTime.Today),
                            owner_id = (int)userId.Value
                        };
                        parsedCustomers.Add(cust);

                        var det = new detail
                        {
                            contact_name = contactName,
                            contact_email = email ?? string.Empty,
                            contact_tel = phone ?? string.Empty,
                            is_active = true,
                            created_at = DateTime.UtcNow,
                            updated_at = DateTime.UtcNow
                        };
                        parsedDetails.Add(det);

                        if (previewRows.Count < 50)
                        {
                            previewRows.Add(new
                            {
                                row = rowIndex,
                                companyName,
                                address,
                                contactName,
                                email,
                                phone,
                                issues = rowIssues
                            });
                        }
                    }
                }
            }

            bool isValid = rowErrors.Count == 0;

            if (isValid && commit)
            {
                using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    for (int i = 0; i < parsedCustomers.Count; i++)
                    {
                        var cust = parsedCustomers[i];
                        var det = parsedDetails[i];

                        _db.customers.Add(cust);
                        await _db.SaveChangesAsync(cancellationToken);

                        det.cust_id = cust.id;
                        _db.details.Add(det);
                    }
                    await _db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return BadRequest(new { message = $"Failed to save customers to database: {ex.Message}" });
                }
            }

            return Ok(new
            {
                isValid,
                totalRows = parsedCustomers.Count,
                errors = rowErrors,
                previewRows
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import customers via manage endpoint");
            return BadRequest(new { message = $"Failed to parse the file: {ex.Message}" });
        }
    }

    [HttpPost("profile")]
    public async Task<IActionResult> ImportProfile(IFormFile file, [FromQuery] bool commit = false, CancellationToken cancellationToken = default)
    {
        var role = User.GetUserRole();
        if (!AppRoles.IsAdminRole(role) && role != AppRoles.Manager)
        {
            return Forbid();
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded or file is empty." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { message = "File size exceeds the 10MB limit." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".csv" && extension != ".xlsx")
        {
            return BadRequest(new { message = "Unsupported file format. Please upload a .csv or .xlsx file." });
        }

        try
        {
            using (var uploadStream = file.OpenReadStream())
            {
                if (!ValidateFileMagicBytes(uploadStream, extension))
                {
                    return BadRequest(new { message = "File content validation failed. The file structure does not match the extension." });
                }
            }

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var columns = new List<string>();
            var parsedRows = new List<profile>();
            var previewRows = new List<object>();
            var rowErrors = new List<object>();
            int rowIndex = 1;

            using (var stream = file.OpenReadStream())
            {
                using (var reader = extension == ".csv"
                    ? ExcelReaderFactory.CreateCsvReader(stream)
                    : ExcelReaderFactory.CreateOpenXmlReader(stream))
                {
                    if (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            columns.Add(reader.GetValue(i)?.ToString()?.Trim() ?? $"Column_{i + 1}");
                        }
                    }
                    else
                    {
                        return BadRequest(new { message = "The uploaded file has no columns or data." });
                    }

                    int nameIdx = columns.FindIndex(c => string.Equals(c, "name", StringComparison.OrdinalIgnoreCase));
                    int typeIdx = columns.FindIndex(c => string.Equals(c, "type", StringComparison.OrdinalIgnoreCase));
                    int itemsIdx = columns.FindIndex(c => string.Equals(c, "items", StringComparison.OrdinalIgnoreCase));
                    int editionsIdx = columns.FindIndex(c => string.Equals(c, "editions", StringComparison.OrdinalIgnoreCase));

                    if (nameIdx == -1 || typeIdx == -1)
                    {
                        return BadRequest(new { message = "Missing required columns. 'name' and 'type' are required." });
                    }

                    while (reader.Read())
                    {
                        rowIndex++;
                        var name = nameIdx < reader.FieldCount ? reader.GetValue(nameIdx)?.ToString()?.Trim() : null;
                        var type = typeIdx < reader.FieldCount ? reader.GetValue(typeIdx)?.ToString()?.Trim() : null;
                        var items = itemsIdx >= 0 && itemsIdx < reader.FieldCount ? reader.GetValue(itemsIdx)?.ToString()?.Trim() : null;
                        var editions = editionsIdx >= 0 && editionsIdx < reader.FieldCount ? reader.GetValue(editionsIdx)?.ToString()?.Trim() : null;

                        var rowIssues = new List<string>();
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            rowIssues.Add("Name is required.");
                        }
                        if (string.IsNullOrWhiteSpace(type))
                        {
                            rowIssues.Add("Type is required.");
                        }

                        if (rowIssues.Count > 0)
                        {
                            rowErrors.Add(new { row = rowIndex, issues = rowIssues });
                        }

                        var p = new profile
                        {
                            name = name ?? string.Empty,
                            type = type ?? "ANTIVIRUS",
                            items = items,
                            editions = editions,
                            is_active = true,
                            created_at = DateTime.UtcNow,
                            updated_at = DateTime.UtcNow
                        };
                        parsedRows.Add(p);

                        if (previewRows.Count < 50)
                        {
                            previewRows.Add(new
                            {
                                row = rowIndex,
                                name,
                                type,
                                items,
                                editions,
                                issues = rowIssues
                            });
                        }
                    }
                }
            }

            bool isValid = rowErrors.Count == 0;

            if (isValid && commit)
            {
                using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    _db.profiles.AddRange(parsedRows);
                    await _db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return BadRequest(new { message = $"Failed to save profiles to database: {ex.Message}" });
                }
            }

            return Ok(new
            {
                isValid,
                totalRows = parsedRows.Count,
                errors = rowErrors,
                previewRows
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import profiles");
            return BadRequest(new { message = $"Failed to parse the file: {ex.Message}" });
        }
    }

    [HttpPost("antivirus-price-list")]
    public async Task<IActionResult> ImportAntivirusPriceList(IFormFile file, [FromQuery] bool commit = false, CancellationToken cancellationToken = default)
    {
        var role = User.GetUserRole();
        if (!AppRoles.IsAdminRole(role) && role != AppRoles.Manager)
        {
            return Forbid();
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded or file is empty." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { message = "File size exceeds the 10MB limit." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".csv" && extension != ".xlsx")
        {
            return BadRequest(new { message = "Unsupported file format. Please upload a .csv or .xlsx file." });
        }

        try
        {
            using (var uploadStream = file.OpenReadStream())
            {
                if (!ValidateFileMagicBytes(uploadStream, extension))
                {
                    return BadRequest(new { message = "File content validation failed. The file structure does not match the extension." });
                }
            }

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var columns = new List<string>();
            var parsedRows = new List<antivirus_price_list>();
            var previewRows = new List<object>();
            var rowErrors = new List<object>();
            int rowIndex = 1;

            var brands = await _db.brands.AsNoTracking().ToListAsync(cancellationToken);

            using (var stream = file.OpenReadStream())
            {
                using (var reader = extension == ".csv"
                    ? ExcelReaderFactory.CreateCsvReader(stream)
                    : ExcelReaderFactory.CreateOpenXmlReader(stream))
                {
                    if (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            columns.Add(reader.GetValue(i)?.ToString()?.Trim() ?? $"Column_{i + 1}");
                        }
                    }
                    else
                    {
                        return BadRequest(new { message = "The uploaded file has no columns or data." });
                    }

                    int codeIdx = columns.FindIndex(c => string.Equals(c, "code", StringComparison.OrdinalIgnoreCase));
                    int startIdx = columns.FindIndex(c => string.Equals(c, "start", StringComparison.OrdinalIgnoreCase));
                    int endIdx = columns.FindIndex(c => string.Equals(c, "end", StringComparison.OrdinalIgnoreCase));
                    int costIdx = columns.FindIndex(c => string.Equals(c, "cost", StringComparison.OrdinalIgnoreCase));

                    if (codeIdx == -1 || startIdx == -1 || endIdx == -1 || costIdx == -1)
                    {
                        return BadRequest(new { message = "Missing required columns. 'code', 'start', 'end', and 'cost' are required." });
                    }

                    while (reader.Read())
                    {
                        rowIndex++;
                        var code = codeIdx < reader.FieldCount ? reader.GetValue(codeIdx)?.ToString()?.Trim() : null;
                        var startVal = startIdx < reader.FieldCount ? reader.GetValue(startIdx)?.ToString()?.Trim() : null;
                        var endVal = endIdx < reader.FieldCount ? reader.GetValue(endIdx)?.ToString()?.Trim() : null;
                        var costVal = costIdx < reader.FieldCount ? reader.GetValue(costIdx)?.ToString()?.Trim() : null;

                        var rowIssues = new List<string>();
                        if (string.IsNullOrWhiteSpace(code))
                        {
                            rowIssues.Add("Code is required.");
                        }

                        int start = 0;
                        if (string.IsNullOrWhiteSpace(startVal))
                        {
                            rowIssues.Add("Start is required.");
                        }
                        else if (!int.TryParse(startVal, out start) || start <= 0)
                        {
                            rowIssues.Add("Start must be a positive integer.");
                        }

                        int end = 0;
                        if (string.IsNullOrWhiteSpace(endVal))
                        {
                            rowIssues.Add("End is required.");
                        }
                        else if (!int.TryParse(endVal, out end) || end <= 0)
                        {
                            rowIssues.Add("End must be a positive integer.");
                        }

                        if (start > 0 && end > 0 && start > end)
                        {
                            rowIssues.Add("Start must be less than or equal to End.");
                        }

                        double cost = 0.0;
                        if (string.IsNullOrWhiteSpace(costVal))
                        {
                            rowIssues.Add("Cost is required.");
                        }
                        else if (!double.TryParse(costVal, out cost) || cost < 0)
                        {
                            rowIssues.Add("Cost must be a non-negative number.");
                        }

                        if (rowIssues.Count > 0)
                        {
                            rowErrors.Add(new { row = rowIndex, issues = rowIssues });
                        }

                        string brandName = "Kaspersky";
                        if (!string.IsNullOrWhiteSpace(code))
                        {
                            var matchedBrand = brands.FirstOrDefault(b => b.name != null &&
                                b.name.Substring(0, Math.Min(b.name.Length, 3)).Equals(code, StringComparison.OrdinalIgnoreCase));
                            if (matchedBrand != null)
                            {
                                brandName = matchedBrand.name;
                            }
                            else
                            {
                                brandName = code;
                            }
                        }

                        var apl = new antivirus_price_list
                        {
                            brand = brandName,
                            code = code,
                            edition = string.Empty,
                            start = start,
                            end = end,
                            cost = cost,
                            types = "Client",
                            created_at = DateTime.UtcNow,
                            updated_at = DateTime.UtcNow
                        };
                        parsedRows.Add(apl);

                        if (previewRows.Count < 50)
                        {
                            previewRows.Add(new
                            {
                                row = rowIndex,
                                code,
                                start = startVal,
                                end = endVal,
                                cost = costVal,
                                issues = rowIssues
                            });
                        }
                    }
                }
            }

            bool isValid = rowErrors.Count == 0;

            if (isValid && commit)
            {
                using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    _db.antivirus_price_lists.AddRange(parsedRows);
                    await _db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return BadRequest(new { message = $"Failed to save antivirus price lists to database: {ex.Message}" });
                }
            }

            return Ok(new
            {
                isValid,
                totalRows = parsedRows.Count,
                errors = rowErrors,
                previewRows
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import antivirus price lists");
            return BadRequest(new { message = $"Failed to parse the file: {ex.Message}" });
        }
    }

    [HttpPost("customers/preview")]
    public async Task<IActionResult> PreviewCustomers(IFormFile file, CancellationToken cancellationToken)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No file uploaded or file is empty." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { message = "File size exceeds the 10MB limit." });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".csv" && extension != ".xlsx")
        {
            return BadRequest(new { message = "Unsupported file format. Please upload a .csv or .xlsx file." });
        }

        try
        {
            // Verify Magic Bytes
            using (var uploadStream = file.OpenReadStream())
            {
                if (!ValidateFileMagicBytes(uploadStream, extension))
                {
                    _logger.LogWarning("File magic bytes verification failed for uploaded file {FileName}", file.FileName);
                    return BadRequest(new { message = "File content validation failed. The file structure does not match the extension." });
                }
            }

            // Staged Upload: Save file locally under a unique GUID
            var fileId = Guid.NewGuid().ToString();
            var tempPath = Path.Combine(GetTempUploadsPath(), $"{fileId}{extension}");
            
            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            _logger.LogInformation("Stored uploaded file {FileName} with ID {FileId}", file.FileName, fileId);

            // Register encoding provider for ExcelDataReader
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            var columns = new List<string>();
            var sampleRows = new List<Dictionary<string, string>>();
            int totalRows = 0;

            using (var stream = System.IO.File.OpenRead(tempPath))
            {
                using (var reader = extension == ".csv" 
                    ? ExcelReaderFactory.CreateCsvReader(stream) 
                    : ExcelReaderFactory.CreateOpenXmlReader(stream))
                {
                    // Read Header Row
                    if (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var headerVal = reader.GetValue(i)?.ToString()?.Trim() ?? $"Column_{i + 1}";
                            columns.Add(headerVal);
                        }
                    }
                    else
                    {
                        // Clean up temporary file
                        System.IO.File.Delete(tempPath);
                        return BadRequest(new { message = "The uploaded file has no columns or data." });
                    }

                    // Read Data Rows and count total
                    while (reader.Read())
                    {
                        totalRows++;

                        // Only load first 50 rows for initial page preview
                        if (totalRows <= 50)
                        {
                            var rowDict = new Dictionary<string, string>();
                            for (int i = 0; i < columns.Count; i++)
                            {
                                var cellVal = i < reader.FieldCount 
                                    ? reader.GetValue(i)?.ToString()?.Trim() ?? "" 
                                    : "";
                                rowDict[columns[i]] = cellVal;
                            }
                            sampleRows.Add(rowDict);
                        }
                    }
                }
            }

            return Ok(new
            {
                fileId = fileId,
                columns = columns,
                sampleRows = sampleRows,
                totalRows = totalRows
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse the file {FileName}", file.FileName);
            return BadRequest(new { message = $"Failed to parse the file: {ex.Message}" });
        }
    }

    [HttpPost("customers/suggest-mappings")]
    public async Task<IActionResult> SuggestMappings([FromBody] List<string> columns)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        if (columns == null || columns.Count == 0)
        {
            return BadRequest(new { message = "Columns list cannot be empty." });
        }

        try
        {
            var result = await _columnMappingService.SuggestMappingsAsync(columns);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to suggest column mappings.");
            return BadRequest(new { message = $"Failed to suggest mappings: {ex.Message}" });
        }
    }

    [HttpPost("customers/extract-unstructured")]
    public async Task<IActionResult> ExtractUnstructured(
        [FromBody] UnstructuredTextRequest request)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        if (request == null || string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new { message = "กรุณาระบุข้อความที่ต้องการแยกข้อมูล" });
        }

        var result = await _aiService.ExtractStructuredDataAsync(request.Text);
        return Ok(result);
    }

    [HttpPost("customers/validate")]
    public async Task<IActionResult> ValidateCustomers(
        [FromBody] List<CustomerImportRow> rows)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        if (rows == null || rows.Count == 0)
        {
            return BadRequest(new { message = "ไม่พบข้อมูลลูกค้าสำหรับตรวจสอบ" });
        }

        var validatedRows = await _validationService.ValidateAndNormalizeRowsAsync(rows);
        return Ok(new
        {
            rows = validatedRows,
            summary = BuildValidationSummary(validatedRows)
        });
    }

    [HttpGet("customers/preview-page")]
    public IActionResult PreviewCustomersPage(
        [FromQuery] string fileId, 
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 50)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(fileId))
        {
            return BadRequest(new { message = "FileId is required." });
        }

        // Find file
        var tempDir = GetTempUploadsPath();
        var tempFile = Directory.GetFiles(tempDir, $"{fileId}.*").FirstOrDefault();
        if (tempFile == null)
        {
            return NotFound(new { message = "Temporary file not found or expired." });
        }

        var extension = Path.GetExtension(tempFile).ToLowerInvariant();

        try
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            var columns = new List<string>();
            var rows = new List<Dictionary<string, string>>();
            int currentRow = 0;
            int startRow = (page - 1) * pageSize + 1;
            int endRow = page * pageSize;

            using (var stream = System.IO.File.OpenRead(tempFile))
            {
                using (var reader = extension == ".csv" 
                    ? ExcelReaderFactory.CreateCsvReader(stream) 
                    : ExcelReaderFactory.CreateOpenXmlReader(stream))
                {
                    // Read Headers
                    if (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            var headerVal = reader.GetValue(i)?.ToString()?.Trim() ?? $"Column_{i + 1}";
                            columns.Add(headerVal);
                        }
                    }

                    // Read Data Rows
                    while (reader.Read())
                    {
                        currentRow++;
                        if (currentRow >= startRow && currentRow <= endRow)
                        {
                            var rowDict = new Dictionary<string, string>();
                            for (int i = 0; i < columns.Count; i++)
                            {
                                var cellVal = i < reader.FieldCount 
                                    ? reader.GetValue(i)?.ToString()?.Trim() ?? "" 
                                    : "";
                                rowDict[columns[i]] = cellVal;
                            }
                            rows.Add(rowDict);
                        }

                        if (currentRow > endRow)
                        {
                            break;
                        }
                    }
                }
            }

            return Ok(rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to paginate preview page {Page} for file {FileId}", page, fileId);
            return BadRequest(new { message = $"Failed to read page: {ex.Message}" });
        }
    }

    [HttpPost("customers/validate-file")]
    public async Task<IActionResult> ValidateFilePage([FromBody] FileValidationRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(request.FileId))
        {
            return BadRequest(new { message = "FileId is required." });
        }

        var tempDir = GetTempUploadsPath();
        var tempFile = Directory.GetFiles(tempDir, $"{request.FileId}.*").FirstOrDefault();
        if (tempFile == null)
        {
            return NotFound(new { message = "Temporary file not found or expired." });
        }

        var extension = Path.GetExtension(tempFile).ToLowerInvariant();

        try
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            var columns = new List<string>();
            var rawRows = new List<Dictionary<string, string>>();
            int currentRow = 0;
            int startRow = (request.Page - 1) * request.PageSize + 1;
            int endRow = request.Page * request.PageSize;

            using (var stream = System.IO.File.OpenRead(tempFile))
            {
                using (var reader = extension == ".csv" 
                    ? ExcelReaderFactory.CreateCsvReader(stream) 
                    : ExcelReaderFactory.CreateOpenXmlReader(stream))
                {
                    if (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            columns.Add(reader.GetValue(i)?.ToString()?.Trim() ?? $"Column_{i + 1}");
                        }
                    }

                    while (reader.Read())
                    {
                        currentRow++;
                        if (currentRow >= startRow && currentRow <= endRow)
                        {
                            var rowDict = new Dictionary<string, string>();
                            for (int i = 0; i < columns.Count; i++)
                            {
                                rowDict[columns[i]] = i < reader.FieldCount ? reader.GetValue(i)?.ToString()?.Trim() ?? "" : "";
                            }
                            rawRows.Add(rowDict);
                        }
                        if (currentRow > endRow) break;
                    }
                }
            }

            // Map columns to CustomerImportRow targets
            var mappedRows = new List<CustomerImportRow>();
            foreach (var raw in rawRows)
            {
                var importRow = new CustomerImportRow();
                foreach (var mapping in request.Mappings)
                {
                    if (string.IsNullOrEmpty(mapping.Value) || !raw.TryGetValue(mapping.Key, out var cellValue))
                    {
                        continue;
                    }

                    switch (mapping.Value.ToLowerInvariant())
                    {
                        case "name": importRow.Name = cellValue; break;
                        case "address": importRow.Address = cellValue; break;
                        case "phone": importRow.Phone = cellValue; break;
                        case "capital": importRow.Capital = cellValue; break;
                        case "business_type": importRow.BusinessType = cellValue; break;
                        case "contact_name": importRow.ContactName = cellValue; break;
                        case "contact_email": importRow.ContactEmail = cellValue; break;
                        case "contact_tel": importRow.ContactTel = cellValue; break;
                        case "contact_position": importRow.ContactPosition = cellValue; break;
                        case "unstructured_company_info": importRow.UnstructuredCompanyInfo = cellValue; break;
                    }
                }
                mappedRows.Add(importRow);
            }

            // Validate mapped rows
            var validationResult = await _validationService.ValidateAndNormalizeRowsAsync(mappedRows);

            // Categorize using Policy Service
            var categorized = _policyService.CategorizeRows(validationResult, request.Policy ?? "Safe", request.MappingConfidence);

            return Ok(new
            {
                rows = validationResult,
                categorized = new
                {
                    autoReady = categorized.AutoReady,
                    needsReview = categorized.NeedsReview,
                    duplicates = categorized.Duplicates,
                    errors = categorized.Errors
                },
                summary = new
                {
                    total = validationResult.Count,
                    autoReadyCount = categorized.AutoReady.Count,
                    needsReviewCount = categorized.NeedsReview.Count,
                    duplicateCount = categorized.Duplicates.Count,
                    errorCount = categorized.Errors.Count
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate file page {Page} for file {FileId}", request.Page, request.FileId);
            return BadRequest(new { message = $"Failed to validate page: {ex.Message}" });
        }
    }

    [HttpPost("customers/explain-issue")]
    public async Task<IActionResult> ExplainIssue([FromBody] IssueExplanationRequest request)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        if (request == null)
        {
            return BadRequest(new { message = "Request body cannot be null." });
        }

        try
        {
            var explanation = await _aiService.ExplainIssueAsync(
                request.IssueType, 
                request.FieldName, 
                request.FieldValue, 
                request.IssueDetails, 
                request.MatchedCustomerDetails
            );
            return Ok(new { explanation });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request issue explanation from AI.");
            return BadRequest(new { message = $"Failed to generate explanation: {ex.Message}" });
        }
    }

    [HttpPost("customers/commit-stream")]
    public async Task CommitCustomersStream(
        [FromBody] StreamCommitRequest request, 
        CancellationToken cancellationToken)
    {
        if (!User.IsAdmin())
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (string.IsNullOrEmpty(request.FileId))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { message = "FileId is required." });
            return;
        }

        var tempDir = GetTempUploadsPath();
        var tempFile = Directory.GetFiles(tempDir, $"{request.FileId}.*").FirstOrDefault();
        if (tempFile == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            await Response.WriteAsJsonAsync(new { message = "Staged file not found." });
            return;
        }

        var extension = Path.GetExtension(tempFile).ToLowerInvariant();
        var userId = User.GetUserId();
        if (userId == null)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        Response.ContentType = "application/json";
        Response.StatusCode = StatusCodes.Status200OK;

        int imported = 0;
        int updated = 0;
        int skipped = 0;
        int totalRows = 0;
        int processedRows = 0;

        _logger.LogInformation("Transaction commit-stream started by user {UserId} for FileId {FileId}", userId.Value, request.FileId);

        // 1. First Pass: Count total rows and register encoding
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var columns = new List<string>();

        using (var countStream = System.IO.File.OpenRead(tempFile))
        {
            using (var reader = extension == ".csv" 
                ? ExcelReaderFactory.CreateCsvReader(countStream) 
                : ExcelReaderFactory.CreateOpenXmlReader(countStream))
            {
                if (reader.Read())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        columns.Add(reader.GetValue(i)?.ToString()?.Trim() ?? $"Column_{i + 1}");
                    }
                }
                while (reader.Read())
                {
                    totalRows++;
                }
            }
        }

        // Setup session
        var session = new import_session
        {
            imported_by = userId.Value,
            file_name = request.FileName ?? Path.GetFileName(tempFile),
            total_rows = totalRows,
            imported_rows = 0,
            skipped_rows = 0,
            error_rows = 0,
            errors_json = "[]",
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        };
        _db.import_sessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        
        try
        {
            // 2. Second Pass: Read and stream commit
            using (var fileStream = System.IO.File.OpenRead(tempFile))
            {
                using (var reader = extension == ".csv" 
                    ? ExcelReaderFactory.CreateCsvReader(fileStream) 
                    : ExcelReaderFactory.CreateOpenXmlReader(fileStream))
                {
                    // Skip header
                    reader.Read();

                    while (reader.Read())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var rawRow = new Dictionary<string, string>();
                        for (int i = 0; i < columns.Count; i++)
                        {
                            rawRow[columns[i]] = i < reader.FieldCount ? reader.GetValue(i)?.ToString()?.Trim() ?? "" : "";
                        }

                        // Map
                        var importRow = new CustomerImportRow();
                        foreach (var mapping in request.Mappings)
                        {
                            if (string.IsNullOrEmpty(mapping.Value) || !rawRow.TryGetValue(mapping.Key, out var val)) continue;

                             switch (mapping.Value.ToLowerInvariant())
                            {
                                case "name": importRow.Name = val; break;
                                case "address": importRow.Address = val; break;
                                case "phone": importRow.Phone = val; break;
                                case "capital": importRow.Capital = val; break;
                                case "business_type": importRow.BusinessType = val; break;
                                case "contact_name": importRow.ContactName = val; break;
                                case "contact_email": importRow.ContactEmail = val; break;
                                case "contact_tel": importRow.ContactTel = val; break;
                                case "contact_position": importRow.ContactPosition = val; break;
                                case "unstructured_company_info": importRow.UnstructuredCompanyInfo = val; break;
                            }
                        }

                        // Validate/Normalize single row
                        var validatedResultList = await _validationService.ValidateAndNormalizeRowsAsync(new List<CustomerImportRow> { importRow });
                        var validatedRow = validatedResultList[0];

                        // Override action check
                        string resolvedAction = validatedRow.Duplicate?.Status == "duplicate" ? "skip" : "new";
                        if (validatedRow.Duplicate?.Status == "warning") resolvedAction = "update";

                        if (request.RowOverrides.TryGetValue(processedRows, out var overrideAct))
                        {
                            resolvedAction = overrideAct;
                        }

                        // Save row state
                        string rowStatus = "Imported";
                        string? rowError = null;

                        if (resolvedAction.Equals("skip", StringComparison.OrdinalIgnoreCase))
                        {
                            rowStatus = "Skipped";
                            skipped++;
                        }
                        else
                        {
                            bool isUpdate = resolvedAction.Equals("update", StringComparison.OrdinalIgnoreCase) && validatedRow.Duplicate?.MatchedCustomerId.HasValue == true;
                            bool updateSuccess = false;

                            if (isUpdate)
                            {
                                var c = await _db.customers.FindAsync(new object[] { validatedRow.Duplicate!.MatchedCustomerId!.Value }, cancellationToken);
                                if (c != null)
                                {
                                    if (!string.IsNullOrWhiteSpace(validatedRow.Name)) c.name = validatedRow.Name;
                                    if (!string.IsNullOrWhiteSpace(validatedRow.Address)) c.address = validatedRow.Address;
                                    if (!string.IsNullOrWhiteSpace(validatedRow.Phone)) c.phone = validatedRow.Phone;
                                    if (validatedRow.Capital.HasValue) c.capital = validatedRow.Capital.Value;

                                    if (!string.IsNullOrWhiteSpace(validatedRow.BusinessType))
                                    {
                                        var bt = await _db.business_types.FirstOrDefaultAsync(b => b.type == validatedRow.BusinessType, cancellationToken);
                                        if (bt != null) c.business_type_id = (int)bt.id;
                                    }

                                    /*
                                    var oldSaleId = c.sale_id;
                                    var oldTelesaleId = c.telesale_id;

                                    if (request.SaleId.HasValue)
                                    {
                                        c.sale_id = request.SaleId.Value;
                                        c.is_assign_sale = true;
                                        c.status = "Assigned";
                                    }
                                    if (request.TelesaleId.HasValue)
                                    {
                                        c.telesale_id = request.TelesaleId.Value;
                                        c.is_assign_telesale = true;
                                        c.status = "Assigned";
                                    }

                                    if (oldSaleId != c.sale_id || oldTelesaleId != c.telesale_id)
                                    {
                                        var history = new assignment_history
                                        {
                                            customer_id = c.id,
                                            old_sale_id = oldSaleId,
                                            new_sale_id = c.sale_id,
                                            old_telesale_id = oldTelesaleId,
                                            new_telesale_id = c.telesale_id,
                                            changed_by_id = userId.Value,
                                            changed_at = DateTime.UtcNow,
                                            reason = "Data Import (Update)"
                                        };
                                        _db.assignment_histories.Add(history);
                                    }
                                    */

                                    c.updated_at = DateTime.UtcNow;
                                    c.updated_user = (int?)userId.Value;

                                    var d = await _db.details.FirstOrDefaultAsync(det => det.cust_id == c.id && det.is_active != false, cancellationToken);
                                    if (d != null)
                                    {
                                        if (!string.IsNullOrWhiteSpace(validatedRow.ContactName)) d.contact_name = validatedRow.ContactName;
                                        if (!string.IsNullOrWhiteSpace(validatedRow.ContactEmail)) d.contact_email = validatedRow.ContactEmail;
                                        if (!string.IsNullOrWhiteSpace(validatedRow.ContactTel)) d.contact_tel = validatedRow.ContactTel;
                                        if (!string.IsNullOrWhiteSpace(validatedRow.ContactPosition)) d.contact_position = validatedRow.ContactPosition;
                                        d.updated_at = DateTime.UtcNow;
                                    }
                                    else if (!string.IsNullOrWhiteSpace(validatedRow.ContactName) || !string.IsNullOrWhiteSpace(validatedRow.ContactEmail) || !string.IsNullOrWhiteSpace(validatedRow.ContactTel) || !string.IsNullOrWhiteSpace(validatedRow.ContactPosition))
                                    {
                                        d = new detail
                                        {
                                            cust_id = c.id,
                                            contact_name = validatedRow.ContactName,
                                            contact_email = validatedRow.ContactEmail ?? "",
                                            contact_tel = validatedRow.ContactTel ?? "",
                                            contact_position = validatedRow.ContactPosition ?? "",
                                            is_active = true,
                                            created_at = DateTime.UtcNow,
                                            updated_at = DateTime.UtcNow
                                        };
                                        _db.details.Add(d);
                                    }

                                    rowStatus = "Updated";
                                    updated++;
                                    updateSuccess = true;
                                }
                            }

                            if (!updateSuccess)
                            {
                                var bt = await _db.business_types.FirstOrDefaultAsync(b => b.type == validatedRow.BusinessType, cancellationToken);
                                var c = new customer
                                {
                                    name = validatedRow.Name,
                                    address = validatedRow.Address,
                                    phone = validatedRow.Phone,
                                    capital = validatedRow.Capital,
                                    business_type_id = bt != null ? (int?)bt.id : null,
                                    status = "New",
                                    create_type = "Import",
                                    is_active = true,
                                    created_at = DateTime.UtcNow,
                                    updated_at = DateTime.UtcNow,
                                    start_dt = DateOnly.FromDateTime(DateTime.Today),
                                    owner_id = (int?)userId.Value
                                };

                                /*
                                if (request.SaleId.HasValue)
                                {
                                    c.sale_id = request.SaleId.Value;
                                    c.is_assign_sale = true;
                                    c.status = "Assigned";
                                }
                                if (request.TelesaleId.HasValue)
                                {
                                    c.telesale_id = request.TelesaleId.Value;
                                    c.is_assign_telesale = true;
                                    c.status = "Assigned";
                                }
                                */

                                _db.customers.Add(c);
                                await _db.SaveChangesAsync(cancellationToken);

                                /*
                                if (c.sale_id.HasValue || c.telesale_id.HasValue)
                                {
                                    var history = new assignment_history
                                    {
                                        customer_id = c.id,
                                        old_sale_id = null,
                                        new_sale_id = c.sale_id,
                                        old_telesale_id = null,
                                        new_telesale_id = c.telesale_id,
                                        changed_by_id = userId.Value,
                                        changed_at = DateTime.UtcNow,
                                        reason = "Data Import (New)"
                                    };
                                    _db.assignment_histories.Add(history);
                                }
                                */

                                if (!string.IsNullOrWhiteSpace(validatedRow.ContactName) || !string.IsNullOrWhiteSpace(validatedRow.ContactEmail) || !string.IsNullOrWhiteSpace(validatedRow.ContactTel) || !string.IsNullOrWhiteSpace(validatedRow.ContactPosition))
                                {
                                    var d = new detail
                                    {
                                        cust_id = c.id,
                                        contact_name = validatedRow.ContactName,
                                        contact_email = validatedRow.ContactEmail ?? "",
                                        contact_tel = validatedRow.ContactTel ?? "",
                                        contact_position = validatedRow.ContactPosition ?? "",
                                        is_active = true,
                                        created_at = DateTime.UtcNow,
                                        updated_at = DateTime.UtcNow
                                    };
                                    _db.details.Add(d);
                                }

                                rowStatus = "Imported";
                                imported++;
                            }
                        }

                        var rowLog = new import_row
                        {
                            session_id = session.id,
                            row_data_json = JsonSerializer.Serialize(validatedRow),
                            status = rowStatus,
                            error_message = rowError,
                            created_at = DateTime.UtcNow
                        };
                        _db.import_rows.Add(rowLog);

                        processedRows++;

                        // Periodically stream progress & save changes
                        if (processedRows % 10 == 0 || processedRows == totalRows)
                        {
                            await _db.SaveChangesAsync(cancellationToken);
                            var msg = new CommitProgressMessage
                            {
                                CurrentProgress = processedRows,
                                TotalRows = totalRows,
                                Status = "Processing",
                                Imported = imported,
                                Updated = updated,
                                Skipped = skipped
                            };
                            await Response.WriteAsync(JsonSerializer.Serialize(msg) + "\n", Encoding.UTF8, cancellationToken);
                            await Response.Body.FlushAsync(cancellationToken);
                        }
                    }
                }
            }

            // Update session values
            session.imported_rows = imported;
            session.skipped_rows = skipped;
            session.updated_at = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Import transaction completed successfully. Mapped total of {TotalRows} rows.", totalRows);

            // Clean up temporary file
            try
            {
                System.IO.File.Delete(tempFile);
            }
            catch (Exception exFile)
            {
                _logger.LogWarning(exFile, "Failed to delete temporary file {TempFile}", tempFile);
            }

            // Yield completed message
            var finalMsg = new CommitProgressMessage
            {
                CurrentProgress = totalRows,
                TotalRows = totalRows,
                Status = "Completed",
                Imported = imported,
                Updated = updated,
                Skipped = skipped
            };
            await Response.WriteAsync(JsonSerializer.Serialize(finalMsg) + "\n", Encoding.UTF8, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(CancellationToken.None); // Rollback without the cancelled token
            _logger.LogError(ex, "Transaction failed for import. Rolling back database changes.");

            // Log session errors outside transaction
            try
            {
                _db.ChangeTracker.Clear();

                var failedSession = await _db.import_sessions
                    .FirstOrDefaultAsync(x => x.id == session.id, CancellationToken.None);

                if (failedSession != null)
                {
                    failedSession.error_rows = totalRows - processedRows;
                    failedSession.errors_json = JsonSerializer.Serialize(new List<string> { ex.Message });
                    failedSession.updated_at = DateTime.UtcNow;

                    await _db.SaveChangesAsync(CancellationToken.None);
                }
            }
            catch (Exception exLog)
            {
                _logger.LogError(exLog, "Failed to write failed import session logs.");
            }

            var failedMsg = new CommitProgressMessage
            {
                CurrentProgress = processedRows,
                TotalRows = totalRows,
                Status = "Failed",
                Imported = imported,
                Updated = updated,
                Skipped = skipped,
                ErrorMessage = ex.Message
            };
            
            try
            {
                await Response.WriteAsync(JsonSerializer.Serialize(failedMsg) + "\n", Encoding.UTF8, CancellationToken.None);
                await Response.Body.FlushAsync(CancellationToken.None);
            }
            catch
            {
                // Connection may be closed already
            }
        }
    }

    [HttpPost("customers/commit")]
    public async Task<IActionResult> CommitCustomers(
        [FromBody] ManualCommitRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        if (request.Rows == null || request.Rows.Count == 0)
        {
            return BadRequest(new { message = "ไม่พบข้อมูลลูกค้าสำหรับนำเข้า" });
        }

        var userId = User.GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var importRows = request.Rows.Select(row => new CustomerImportRow
        {
            Name = row.Name,
            Address = row.Address,
            Phone = row.Phone,
            Capital = row.Capital?.ToString(CultureInfo.InvariantCulture),
            BusinessType = row.BusinessType,
            ContactName = row.ContactName,
            ContactEmail = row.ContactEmail,
            ContactTel = row.ContactTel,
            ContactPosition = row.ContactPosition
        }).ToList();

        var validatedRows = await _validationService.ValidateAndNormalizeRowsAsync(importRows);
        var imported = 0;
        var updated = 0;
        var skipped = 0;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var session = new import_session
            {
                imported_by = userId.Value,
                file_name = request.FileName ?? "Unstructured Text Extract",
                total_rows = request.Rows.Count,
                imported_rows = 0,
                skipped_rows = 0,
                error_rows = 0,
                errors_json = "[]",
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };
            _db.import_sessions.Add(session);
            await _db.SaveChangesAsync(cancellationToken);

            for (var index = 0; index < validatedRows.Count; index++)
            {
                var sourceRow = request.Rows[index];
                var row = validatedRows[index];
                var action = sourceRow.ImportAction?.Trim().ToLowerInvariant() ?? "new";
                var rowStatus = "Imported";
                string? rowError = null;

                if (action == "skip" || row.Status == "error")
                {
                    skipped++;
                    rowStatus = "Skipped";
                    rowError = row.Status == "error"
                        ? string.Join(" | ", row.Issues.Select(issue => issue.Message))
                        : null;
                }
                else
                {
                    var matchedCustomerId = sourceRow.MatchedCustomerId ?? row.Duplicate?.MatchedCustomerId;
                    var existingCustomer = action == "update" && matchedCustomerId.HasValue
                        ? await _db.customers.FindAsync(new object[] { matchedCustomerId.Value }, cancellationToken)
                        : null;

                    if (existingCustomer != null)
                    {
                        await UpdateImportedCustomerAsync(
                            existingCustomer,
                            row,
                            request.SaleId,
                            request.TelesaleId,
                            userId.Value,
                            cancellationToken);
                        updated++;
                        rowStatus = "Updated";
                    }
                    else
                    {
                        await CreateImportedCustomerAsync(
                            row,
                            request.SaleId,
                            request.TelesaleId,
                            userId.Value,
                            cancellationToken);
                        imported++;
                    }
                }

                _db.import_rows.Add(new import_row
                {
                    session_id = session.id,
                    row_data_json = JsonSerializer.Serialize(row),
                    status = rowStatus,
                    error_message = rowError,
                    created_at = DateTime.UtcNow
                });
            }

            session.imported_rows = imported;
            session.skipped_rows = skipped;
            session.error_rows = validatedRows.Count(row => row.Status == "error");
            session.updated_at = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(new { imported, updated, skipped });
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    [HttpGet("customers/export-errors")]
    public async Task<IActionResult> ExportValidationErrors([FromQuery] string fileId, [FromQuery] string mappingsJson)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        if (string.IsNullOrEmpty(fileId) || string.IsNullOrEmpty(mappingsJson))
        {
            return BadRequest(new { message = "FileId and Mappings are required." });
        }

        var tempDir = GetTempUploadsPath();
        var tempFile = Directory.GetFiles(tempDir, $"{fileId}.*").FirstOrDefault();
        if (tempFile == null)
        {
            return NotFound(new { message = "Temporary file not found or expired." });
        }

        var extension = Path.GetExtension(tempFile).ToLowerInvariant();

        try
        {
            var mappings = JsonSerializer.Deserialize<Dictionary<string, string>>(mappingsJson);
            if (mappings == null) return BadRequest(new { message = "Invalid mappings payload format." });

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            var columns = new List<string>();
            var errorReport = new StringBuilder();
            
            // CSV Header
            errorReport.AppendLine("Row Number,Validation Errors,Company Name,Address,Phone,Capital,Business Type,Contact Name,Contact Email,Contact Tel,Contact Position");

            int currentRow = 0;

            using (var stream = System.IO.File.OpenRead(tempFile))
            {
                using (var reader = extension == ".csv" 
                    ? ExcelReaderFactory.CreateCsvReader(stream) 
                    : ExcelReaderFactory.CreateOpenXmlReader(stream))
                {
                    if (reader.Read())
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            columns.Add(reader.GetValue(i)?.ToString()?.Trim() ?? $"Column_{i + 1}");
                        }
                    }

                    while (reader.Read())
                    {
                        currentRow++;
                        var rawRow = new Dictionary<string, string>();
                        for (int i = 0; i < columns.Count; i++)
                        {
                            rawRow[columns[i]] = i < reader.FieldCount ? reader.GetValue(i)?.ToString()?.Trim() ?? "" : "";
                        }

                        // Map
                        var importRow = new CustomerImportRow();
                        foreach (var mapping in mappings)
                        {
                            if (string.IsNullOrEmpty(mapping.Value) || !rawRow.TryGetValue(mapping.Key, out var val)) continue;

                            switch (mapping.Value.ToLowerInvariant())
                            {
                                case "name": importRow.Name = val; break;
                                case "address": importRow.Address = val; break;
                                case "phone": importRow.Phone = val; break;
                                case "capital": importRow.Capital = val; break;
                                case "business_type": importRow.BusinessType = val; break;
                                case "contact_name": importRow.ContactName = val; break;
                                case "contact_email": importRow.ContactEmail = val; break;
                                case "contact_tel": importRow.ContactTel = val; break;
                                case "contact_position": importRow.ContactPosition = val; break;
                            }
                        }

                        // Validate
                        var validatedResultList = await _validationService.ValidateAndNormalizeRowsAsync(new List<CustomerImportRow> { importRow });
                        var validatedRow = validatedResultList[0];

                        if (validatedRow.Status == "error" || validatedRow.Status == "warning")
                        {
                            var errorsList = string.Join(" | ", validatedRow.Issues.Select(i => $"[{i.Field}]: {i.Message}"));
                            
                            // Safe CSV escaping helper
                            string EscapeCsv(string? val)
                            {
                                if (string.IsNullOrEmpty(val)) return "";
                                if (val.Contains(",") || val.Contains("\"") || val.Contains("\n") || val.Contains("\r"))
                                {
                                    return $"\"{val.Replace("\"", "\"\"")}\"";
                                }
                                return val;
                            }

                            errorReport.AppendLine($"{currentRow},{EscapeCsv(errorsList)},{EscapeCsv(importRow.Name)},{EscapeCsv(importRow.Address)},{EscapeCsv(importRow.Phone)},{EscapeCsv(importRow.Capital)},{EscapeCsv(importRow.BusinessType)},{EscapeCsv(importRow.ContactName)},{EscapeCsv(importRow.ContactEmail)},{EscapeCsv(importRow.ContactTel)},{EscapeCsv(importRow.ContactPosition)}");
                        }
                    }
                }
            }

            var csvBytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(errorReport.ToString())).ToArray();
            return File(csvBytes, "text/csv", "import_validation_errors.csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate validation errors report for FileId {FileId}", fileId);
            return BadRequest(new { message = $"Failed to generate report: {ex.Message}" });
        }
    }

    private static object BuildValidationSummary(List<ValidatedRowResult> rows)
    {
        return new
        {
            total = rows.Count,
            valid = rows.Count(row => row.Status == "valid"),
            warning = rows.Count(row => row.Status == "warning"),
            error = rows.Count(row => row.Status == "error"),
            duplicateCount = rows.Count(row => row.Duplicate?.Status == "duplicate"),
            duplicateWarningCount = rows.Count(row => row.Duplicate?.Status == "warning"),
            uniqueCount = rows.Count(row => row.Duplicate?.Status == "unique")
        };
    }

    private async Task UpdateImportedCustomerAsync(
        customer customer,
        ValidatedRowResult row,
        int? saleId,
        int? telesaleId,
        uint userId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(row.Name)) customer.name = row.Name;
        if (!string.IsNullOrWhiteSpace(row.Address)) customer.address = row.Address;
        if (!string.IsNullOrWhiteSpace(row.Phone)) customer.phone = row.Phone;
        if (row.Capital.HasValue) customer.capital = row.Capital.Value;

        if (!string.IsNullOrWhiteSpace(row.BusinessType))
        {
            var businessType = await _db.business_types
                .FirstOrDefaultAsync(item => item.type == row.BusinessType, cancellationToken);
            if (businessType != null)
            {
                customer.business_type_id = (int)businessType.id;
            }
        }

        ApplyImportAssignments(customer, saleId, telesaleId);
        customer.updated_user = (int)userId;
        customer.updated_at = DateTime.UtcNow;

        var contact = await _db.details
            .FirstOrDefaultAsync(item => item.cust_id == customer.id && item.is_active != false, cancellationToken);
        if (contact == null && HasContactData(row))
        {
            contact = new detail
            {
                cust_id = customer.id,
                is_active = true,
                created_at = DateTime.UtcNow
            };
            _db.details.Add(contact);
        }

        if (contact != null)
        {
            ApplyImportedContact(contact, row);
        }
    }

    private async Task CreateImportedCustomerAsync(
        ValidatedRowResult row,
        int? saleId,
        int? telesaleId,
        uint userId,
        CancellationToken cancellationToken)
    {
        var businessType = await _db.business_types
            .FirstOrDefaultAsync(item => item.type == row.BusinessType, cancellationToken);
        var customer = new customer
        {
            name = row.Name,
            address = row.Address,
            phone = row.Phone,
            capital = row.Capital,
            business_type_id = businessType != null ? (int)businessType.id : null,
            status = "New",
            create_type = "Import",
            is_active = true,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow,
            start_dt = DateOnly.FromDateTime(DateTime.Today),
            owner_id = (int)userId
        };
        ApplyImportAssignments(customer, saleId, telesaleId);
        _db.customers.Add(customer);
        await _db.SaveChangesAsync(cancellationToken);

        if (HasContactData(row))
        {
            var contact = new detail
            {
                cust_id = customer.id,
                is_active = true,
                created_at = DateTime.UtcNow
            };
            ApplyImportedContact(contact, row);
            _db.details.Add(contact);
        }
    }

    private static void ApplyImportAssignments(customer customer, int? saleId, int? telesaleId)
    {
        // Assignment is no longer supported
    }

    private static bool HasContactData(ValidatedRowResult row)
    {
        return !string.IsNullOrWhiteSpace(row.ContactName)
            || !string.IsNullOrWhiteSpace(row.ContactEmail)
            || !string.IsNullOrWhiteSpace(row.ContactTel)
            || !string.IsNullOrWhiteSpace(row.ContactPosition);
    }

    private static void ApplyImportedContact(detail contact, ValidatedRowResult row)
    {
        if (!string.IsNullOrWhiteSpace(row.ContactName)) contact.contact_name = row.ContactName;
        if (!string.IsNullOrWhiteSpace(row.ContactEmail)) contact.contact_email = row.ContactEmail;
        if (!string.IsNullOrWhiteSpace(row.ContactTel)) contact.contact_tel = row.ContactTel;
        if (!string.IsNullOrWhiteSpace(row.ContactPosition)) contact.contact_position = row.ContactPosition;
        contact.updated_at = DateTime.UtcNow;
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetImportHistory(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!User.IsAdmin())
        {
            return Forbid();
        }

        var query = _db.import_sessions
            .AsNoTracking()
            .Join(_db.users,
                s => s.imported_by,
                u => u.id,
                (s, u) => new
                {
                    id = s.id,
                    importedBy = u.name ?? u.username,
                    fileName = s.file_name,
                    totalRows = s.total_rows,
                    importedRows = s.imported_rows,
                    skippedRows = s.skipped_rows,
                    errorRows = s.error_rows,
                    errorsJson = s.errors_json,
                    createdAt = s.created_at
                });

        var orderedQuery = query.OrderByDescending(x => x.createdAt);

        if (page.HasValue)
        {
            var size = pageSize ?? 10;
            if (size <= 0) return BadRequest("Page size must be greater than zero.");
            if (size > 100) return BadRequest("Page size cannot exceed 100.");
            if (page.Value <= 0) return BadRequest("Page number must be greater than zero.");

            var totalCount = await orderedQuery.CountAsync(cancellationToken);
            var totalPages = (int)Math.Ceiling((double)totalCount / size);

            var items = await orderedQuery
                .Skip((page.Value - 1) * size)
                .Take(size)
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                items,
                totalCount,
                page = page.Value,
                pageSize = size,
                totalPages
            });
        }
        else
        {
            var items = await orderedQuery.ToListAsync(cancellationToken);
            return Ok(items);
        }
    }
}

public class UnstructuredTextRequest
{
    public string Text { get; set; } = string.Empty;
}

public class FileValidationRequest
{
    public string FileId { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public Dictionary<string, string> Mappings { get; set; } = new();
    public string? Policy { get; set; }
    public double? MappingConfidence { get; set; }
}

public class IssueExplanationRequest
{
    public string IssueType { get; set; } = string.Empty; // "validation", "duplicate", "business_type"
    public string FieldName { get; set; } = string.Empty;
    public string FieldValue { get; set; } = string.Empty;
    public string IssueDetails { get; set; } = string.Empty;
    public string? MatchedCustomerDetails { get; set; }
}

public class StreamCommitRequest
{
    public string FileId { get; set; } = string.Empty;
    public Dictionary<string, string> Mappings { get; set; } = new();
    public int? SaleId { get; set; }
    public int? TelesaleId { get; set; }
    public string? FileName { get; set; }
    public Dictionary<int, string> RowOverrides { get; set; } = new(); // 0-indexed row index -> "new"|"update"|"skip"
    public string? Policy { get; set; }
}

public class ManualCommitRequest
{
    public List<ManualImportRow> Rows { get; set; } = new();
    public int? SaleId { get; set; }
    public int? TelesaleId { get; set; }
    public string? FileName { get; set; }
}

public class ManualImportRow
{
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public double? Capital { get; set; }
    public string? BusinessType { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactTel { get; set; }
    public string? ContactPosition { get; set; }
    public string? ImportAction { get; set; }
    public uint? MatchedCustomerId { get; set; }
}

public class CommitProgressMessage
{
    public int CurrentProgress { get; set; }
    public int TotalRows { get; set; }
    public string Status { get; set; } = "Processing"; // "Processing", "Completed", "Failed"
    public int Imported { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public string? ErrorMessage { get; set; }
}
