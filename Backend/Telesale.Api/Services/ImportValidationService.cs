using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;

namespace Telesale.Api.Services;

public class ImportValidationService : IImportValidationService
{
    private readonly TelesaleDbContext _db;
    private readonly IImportDuplicateDetectionService _duplicateService;
    private readonly IImportNormalizationService _normalizationService;
    private readonly IImportAiExtractionService _aiExtractionService;

    public ImportValidationService(
        TelesaleDbContext db, 
        IImportDuplicateDetectionService duplicateService,
        IImportNormalizationService normalizationService,
        IImportAiExtractionService aiExtractionService)
    {
        _db = db;
        _duplicateService = duplicateService;
        _normalizationService = normalizationService;
        _aiExtractionService = aiExtractionService;
    }

    public async Task<List<ValidatedRowResult>> ValidateAndNormalizeRowsAsync(List<CustomerImportRow> rows)
    {
        var result = new List<ValidatedRowResult>();

        if (rows == null || rows.Count == 0)
        {
            return result;
        }

        // Fetch active business types from DB for lookup verification
        var activeBusinessTypes = await _db.business_types
            .AsNoTracking()
            .Where(bt => bt.is_active == null || bt.is_active == true)
            .Select(bt => bt.type)
            .ToListAsync();

        var activeBtSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in activeBusinessTypes)
        {
            if (!string.IsNullOrWhiteSpace(type))
            {
                activeBtSet.Add(type.Trim());
            }
        }

        // Email validation regex
        var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");

        foreach (var row in rows)
        {
            var validatedRow = new ValidatedRowResult();
            validatedRow.Confidence = 1.0; // Default: fully confident (mapped columns)

            // 1. Unstructured AI Extraction & Auto-fill
            ExtractionResult? extracted = null;
            if (!string.IsNullOrWhiteSpace(row.UnstructuredCompanyInfo))
            {
                extracted = await _aiExtractionService.ExtractStructuredDataAsync(row.UnstructuredCompanyInfo);
                if (extracted != null)
                {
                    validatedRow.Confidence = extracted.Confidence;
                }
            }

            // Helper to fill and tag metadata
            void EnrichField(string fieldName, string? mappedValue, string? extractedValue, Action<string?> setter)
            {
                var cleanedMapped = _normalizationService.CleanText(mappedValue);
                if (!string.IsNullOrEmpty(cleanedMapped))
                {
                    setter(cleanedMapped);
                    validatedRow.ExtractionMetadata[fieldName] = new FieldExtractionMetadata
                    {
                        Source = "MappedColumn",
                        Confidence = 1.0
                    };
                }
                else if (extracted != null && !string.IsNullOrEmpty(extractedValue))
                {
                    setter(_normalizationService.CleanText(extractedValue));
                    double fieldConfidence = 0.85;
                    if (extracted.FieldConfidences != null && extracted.FieldConfidences.TryGetValue(fieldName, out var conf))
                    {
                        fieldConfidence = conf;
                    }
                    validatedRow.ExtractionMetadata[fieldName] = new FieldExtractionMetadata
                    {
                        Source = "AiExtraction",
                        Confidence = fieldConfidence
                    };
                }
                else
                {
                    setter(null);
                }
            }

            // Enrich all textual fields
            EnrichField("name", row.Name, extracted?.Name, val => validatedRow.Name = val);
            EnrichField("address", row.Address, extracted?.Address, val => validatedRow.Address = val);
            EnrichField("business_type", row.BusinessType, extracted?.BusinessType, val => validatedRow.BusinessType = val);
            EnrichField("contact_name", row.ContactName, extracted?.ContactName, val => validatedRow.ContactName = val);
            EnrichField("contact_email", row.ContactEmail, extracted?.ContactEmail, val => validatedRow.ContactEmail = val);
            EnrichField("contact_position", row.ContactPosition, extracted?.ContactPosition, val => validatedRow.ContactPosition = val);
            validatedRow.Code = _normalizationService.CleanText(row.Code);

            // Enrich and Normalize Phone Numbers
            string? rawPhone = _normalizationService.CleanText(row.Phone);
            if (!string.IsNullOrEmpty(rawPhone))
            {
                validatedRow.Phone = _normalizationService.NormalizePhoneNumber(rawPhone);
                validatedRow.ExtractionMetadata["phone"] = new FieldExtractionMetadata { Source = "MappedColumn", Confidence = 1.0 };
            }
            else if (extracted != null && !string.IsNullOrEmpty(extracted?.Phone))
            {
                validatedRow.Phone = _normalizationService.NormalizePhoneNumber(extracted.Phone);
                double fieldConfidence = extracted.FieldConfidences != null && extracted.FieldConfidences.TryGetValue("phone", out var conf) ? conf : 0.85;
                validatedRow.ExtractionMetadata["phone"] = new FieldExtractionMetadata { Source = "AiExtraction", Confidence = fieldConfidence };
            }

            string? rawContactTel = _normalizationService.CleanText(row.ContactTel);
            if (!string.IsNullOrEmpty(rawContactTel))
            {
                validatedRow.ContactTel = _normalizationService.NormalizePhoneNumber(rawContactTel);
                validatedRow.ExtractionMetadata["contact_tel"] = new FieldExtractionMetadata { Source = "MappedColumn", Confidence = 1.0 };
            }
            else if (extracted != null && !string.IsNullOrEmpty(extracted?.ContactTel))
            {
                validatedRow.ContactTel = _normalizationService.NormalizePhoneNumber(extracted.ContactTel);
                double fieldConfidence = extracted.FieldConfidences != null && extracted.FieldConfidences.TryGetValue("contact_tel", out var conf) ? conf : 0.85;
                validatedRow.ExtractionMetadata["contact_tel"] = new FieldExtractionMetadata { Source = "AiExtraction", Confidence = fieldConfidence };
            }

            // Enrich and Normalize Capital
            string? rawCapital = _normalizationService.CleanText(row.Capital);
            if (!string.IsNullOrEmpty(rawCapital))
            {
                var normalizedCapString = _normalizationService.NormalizeCapital(rawCapital);
                if (double.TryParse(normalizedCapString, out var parsedCapital))
                {
                    validatedRow.Capital = parsedCapital;
                }
                validatedRow.ExtractionMetadata["capital"] = new FieldExtractionMetadata { Source = "MappedColumn", Confidence = 1.0 };
            }
            else if (extracted != null && extracted.Capital.HasValue)
            {
                validatedRow.Capital = extracted.Capital.Value;
                double fieldConfidence = extracted.FieldConfidences != null && extracted.FieldConfidences.TryGetValue("capital", out var conf) ? conf : 0.85;
                validatedRow.ExtractionMetadata["capital"] = new FieldExtractionMetadata { Source = "AiExtraction", Confidence = fieldConfidence };
            }

            // 2. Validation Rules
            // Rule 1: Customer Name is required
            if (string.IsNullOrWhiteSpace(validatedRow.Name))
            {
                validatedRow.Issues.Add(new ValidationErrorItem
                {
                    Field = "name",
                    Message = "Company Name is required.",
                    Severity = "error"
                });
            }

            // Rule 2: Phone valid format if provided (9-10 digits after normalization)
            if (!string.IsNullOrEmpty(validatedRow.Phone))
            {
                if (!Regex.IsMatch(validatedRow.Phone, @"^\d{9,10}$"))
                {
                    var sourceVal = validatedRow.ExtractionMetadata.ContainsKey("phone") && validatedRow.ExtractionMetadata["phone"].Source == "AiExtraction"
                        ? extracted?.Phone
                        : row.Phone;
                    validatedRow.Issues.Add(new ValidationErrorItem
                    {
                        Field = "phone",
                        Message = $"Phone format is invalid (detected '{sourceVal}'). Must be a 9 or 10-digit number.",
                        Severity = "warning"
                    });
                }
            }

            // Rule 3: Capital numeric only
            var currentRawCapital = validatedRow.ExtractionMetadata.ContainsKey("capital") && validatedRow.ExtractionMetadata["capital"].Source == "AiExtraction"
                ? extracted?.Capital?.ToString()
                : row.Capital;
            if (!string.IsNullOrEmpty(currentRawCapital) && !validatedRow.Capital.HasValue)
            {
                validatedRow.Issues.Add(new ValidationErrorItem
                {
                    Field = "capital",
                    Message = $"Capital value '{currentRawCapital}' is invalid. Must be numeric.",
                    Severity = "error"
                });
            }

            // Rule 4: Business Type must resolve to existing type
            if (string.IsNullOrWhiteSpace(validatedRow.BusinessType))
            {
                validatedRow.Issues.Add(new ValidationErrorItem
                {
                    Field = "business_type",
                    Message = "Business Type is required.",
                    Severity = "error"
                });
            }
            else if (!activeBtSet.Contains(validatedRow.BusinessType))
            {
                var sourceVal = validatedRow.ExtractionMetadata.ContainsKey("business_type") && validatedRow.ExtractionMetadata["business_type"].Source == "AiExtraction"
                    ? extracted?.BusinessType
                    : row.BusinessType;
                validatedRow.Issues.Add(new ValidationErrorItem
                {
                    Field = "business_type",
                    Message = $"Business Type '{sourceVal}' does not match any category in the database.",
                    Severity = "error"
                });
            }

            // Rule 5: Contact Email format if provided
            if (!string.IsNullOrEmpty(validatedRow.ContactEmail))
            {
                if (!emailRegex.IsMatch(validatedRow.ContactEmail))
                {
                    validatedRow.Issues.Add(new ValidationErrorItem
                    {
                        Field = "contact_email",
                        Message = $"Contact Email '{validatedRow.ContactEmail}' is in an invalid format.",
                        Severity = "warning"
                    });
                }
            }

            // Rule 6: Contact Tel format if provided
            if (!string.IsNullOrEmpty(validatedRow.ContactTel))
            {
                if (!Regex.IsMatch(validatedRow.ContactTel, @"^\d{9,10}$"))
                {
                    var sourceVal = validatedRow.ExtractionMetadata.ContainsKey("contact_tel") && validatedRow.ExtractionMetadata["contact_tel"].Source == "AiExtraction"
                        ? extracted?.ContactTel
                        : row.ContactTel;
                    validatedRow.Issues.Add(new ValidationErrorItem
                    {
                        Field = "contact_tel",
                        Message = $"Contact Phone is invalid (detected '{sourceVal}'). Must be 9 or 10 digits.",
                        Severity = "warning"
                    });
                }
            }

            // Determine Overall Row Status
            if (validatedRow.Issues.Any(i => i.Severity == "error"))
            {
                validatedRow.Status = "error";
            }
            else if (validatedRow.Issues.Any(i => i.Severity == "warning"))
            {
                validatedRow.Status = "warning";
            }
            else
            {
                validatedRow.Status = "valid";
            }

            // 3. Duplicate Detection
            var dupResult = await _duplicateService.DetectDuplicateAsync(validatedRow.Name, validatedRow.Phone, validatedRow.Code);
            validatedRow.Duplicate = dupResult;

            // 4. Generate Telesales Suggestions
            validatedRow.SuggestedStatus = "New";
            
            // Priority: High if capital >= 5,000,000, Low if capital < 1,000,000, otherwise Medium
            if (validatedRow.Capital.HasValue && validatedRow.Capital.Value >= 5000000)
            {
                validatedRow.SuggestedPriority = "High";
                validatedRow.SuggestedFollowUpDays = 3;
            }
            else if (validatedRow.Capital.HasValue && validatedRow.Capital.Value < 1000000)
            {
                validatedRow.SuggestedPriority = "Low";
                validatedRow.SuggestedFollowUpDays = 14;
            }
            else
            {
                validatedRow.SuggestedPriority = "Medium";
                validatedRow.SuggestedFollowUpDays = 7;
            }

            // Call angle: suggest pitch based on business type
            var btLower = validatedRow.BusinessType?.ToLowerInvariant() ?? "";
            if (btLower.Contains("it") || btLower.Contains("technology") || btLower.Contains("security") || btLower.Contains("software"))
            {
                validatedRow.SuggestedCallAngle = "Highlight our IT security solutions, cloud backup, and enterprise digital threat assessments.";
            }
            else if (btLower.Contains("trade") || btLower.Contains("import") || btLower.Contains("export") || btLower.Contains("logistics"))
            {
                validatedRow.SuggestedCallAngle = "Pitch logistics supply chain optimization, import/export cargo insurance, and wholesale vendor packages.";
            }
            else if (btLower.Contains("manufactur") || btLower.Contains("factory") || btLower.Contains("industry"))
            {
                validatedRow.SuggestedCallAngle = "Focus on industrial equipment financing, supply chain raw materials renewals, and volume discounts.";
            }
            else
            {
                validatedRow.SuggestedCallAngle = "Pitch standard renewals, customer loyalty discount packages, and basic support benefits.";
            }

            result.Add(validatedRow);
        }

        return result;
    }
}
