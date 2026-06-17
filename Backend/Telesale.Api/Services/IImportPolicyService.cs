using System.Collections.Generic;

namespace Telesale.Api.Services;

public class PolicyCategorizedRows
{
    public List<ValidatedRowResult> AutoReady { get; set; } = new();
    public List<ValidatedRowResult> NeedsReview { get; set; } = new();
    public List<ValidatedRowResult> Duplicates { get; set; } = new();
    public List<ValidatedRowResult> Errors { get; set; } = new();
}

public interface IImportPolicyService
{
    PolicyCategorizedRows CategorizeRows(List<ValidatedRowResult> rows, string policy, double? mappingConfidence);
}
