using System.Collections.Generic;
using System.Threading.Tasks;

namespace Telesale.Api.Services;

public class CustomerImportRow
{
    public string? Name { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Capital { get; set; }
    public string? BusinessType { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactTel { get; set; }
    public string? ContactPosition { get; set; }
    public string? Code { get; set; }
    public string? UnstructuredCompanyInfo { get; set; }
}

public class ValidationErrorItem
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // "error" or "warning"
}

public class FieldExtractionMetadata
{
    public string Source { get; set; } = "MappedColumn"; // "MappedColumn" or "AiExtraction"
    public double Confidence { get; set; } = 1.0;
}

public class ValidatedRowResult
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
    public string? Code { get; set; }

    public string Status { get; set; } = "valid"; // "valid", "warning", "error"
    public List<ValidationErrorItem> Issues { get; set; } = new();
    public DuplicateMatchResult? Duplicate { get; set; }

    public double? Confidence { get; set; }
    public Dictionary<string, FieldExtractionMetadata> ExtractionMetadata { get; set; } = new();

    public string? SuggestedStatus { get; set; }
    public string? SuggestedPriority { get; set; }
    public int? SuggestedFollowUpDays { get; set; }
    public string? SuggestedCallAngle { get; set; }
}

public interface IImportValidationService
{
    Task<List<ValidatedRowResult>> ValidateAndNormalizeRowsAsync(List<CustomerImportRow> rows);
}
