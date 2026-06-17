using System;
using System.Collections.Generic;

namespace Telesale.Api.Services;

public class ImportPolicyService : IImportPolicyService
{
    public PolicyCategorizedRows CategorizeRows(List<ValidatedRowResult> rows, string policy, double? mappingConfidence)
    {
        var result = new PolicyCategorizedRows();
        if (rows == null) return result;

        var currentPolicy = string.IsNullOrWhiteSpace(policy) ? "Safe" : policy;
        double confidence = mappingConfidence ?? 1.0;

        foreach (var row in rows)
        {
            // 1. Errors always go to Errors tab
            if (row.Status == "error")
            {
                result.Errors.Add(row);
                continue;
            }

            // Categorize by policy
            if (currentPolicy.Equals("Strict", StringComparison.OrdinalIgnoreCase))
            {
                // Strict Mode: everything goes to Needs Review or Duplicates, no AutoReady
                if (row.Duplicate != null && row.Duplicate.Status != "unique")
                {
                    result.Duplicates.Add(row);
                }
                else
                {
                    result.NeedsReview.Add(row);
                }
            }
            else if (currentPolicy.Equals("Fast", StringComparison.OrdinalIgnoreCase))
            {
                // Fast Mode:
                // - Auto-import valid rows without duplicate.
                // - Review only errors and fuzzy duplicates.
                if (row.Duplicate != null && row.Duplicate.Status == "warning")
                {
                    // Fuzzy duplicate
                    result.Duplicates.Add(row);
                }
                else
                {
                    // No duplicate (unique) or exact duplicate (auto-imported or handled)
                    if (row.Duplicate != null && row.Duplicate.Status == "duplicate")
                    {
                        // exact duplicate
                        result.Duplicates.Add(row);
                    }
                    else
                    {
                        result.AutoReady.Add(row);
                    }
                }
            }
            else
            {
                // Safe Mode (Default):
                // - Auto-import rows with validation status valid, confidence >= 0.90, and no duplicate.
                // - Review warning/error/duplicate rows.
                bool isUnique = row.Duplicate == null || row.Duplicate.Status == "unique";
                double rowConfidence = row.Confidence ?? 1.0;

                if (row.Status == "valid" && isUnique && rowConfidence >= 0.90 && confidence >= 0.90)
                {
                    result.AutoReady.Add(row);
                }
                else if (row.Duplicate != null && row.Duplicate.Status != "unique")
                {
                    result.Duplicates.Add(row);
                }
                else
                {
                    result.NeedsReview.Add(row);
                }
            }
        }

        return result;
    }
}
