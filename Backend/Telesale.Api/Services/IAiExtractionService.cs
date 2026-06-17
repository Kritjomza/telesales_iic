using System.Collections.Generic;
using System.Threading.Tasks;

namespace Telesale.Api.Services;

public class ColumnMappingItem
{
    public string Column { get; set; } = string.Empty;
    public string TargetField { get; set; } = string.Empty; // e.g. "name", "address", etc.
}

public class ColumnMappingResult
{
    public List<ColumnMappingItem> Mappings { get; set; } = new();
    public double Confidence { get; set; }
}

public class ExtractionResult
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
    public double Confidence { get; set; }
    public Dictionary<string, double> FieldConfidences { get; set; } = new();
}

public interface IAiExtractionService
{
    Task<ColumnMappingResult> SuggestMappingsAsync(List<string> columns);
    Task<ExtractionResult> ExtractStructuredDataAsync(string unstructuredText);
    Task<string> ExplainIssueAsync(string issueType, string fieldName, string fieldValue, string issueDetails, string? matchedCustomerDetails);
}
