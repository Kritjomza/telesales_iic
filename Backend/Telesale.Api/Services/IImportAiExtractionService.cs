using System.Threading.Tasks;

namespace Telesale.Api.Services;

public interface IImportAiExtractionService
{
    Task<ExtractionResult> ExtractStructuredDataAsync(string unstructuredText);
    Task<string> ExplainIssueAsync(string issueType, string fieldName, string fieldValue, string issueDetails, string? matchedCustomerDetails);
}
