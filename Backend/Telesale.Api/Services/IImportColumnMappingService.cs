using System.Collections.Generic;
using System.Threading.Tasks;

namespace Telesale.Api.Services;

public class SuggestedMappingItem
{
    public string Column { get; set; } = string.Empty;
    public string TargetField { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Status { get; set; } = "manual_required"; // "auto_accepted", "review_required", "manual_required"
}

public class SuggestedMappingResult
{
    public List<SuggestedMappingItem> Mappings { get; set; } = new();
    public double Confidence { get; set; }
}

public interface IImportColumnMappingService
{
    Task<SuggestedMappingResult> SuggestMappingsAsync(List<string> columns);
}
