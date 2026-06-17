using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Telesale.Api.Services;

public class ImportAiExtractionService : IImportAiExtractionService
{
    private readonly AiProviderFactory _providerFactory;
    private readonly ILogger<ImportAiExtractionService> _logger;

    public ImportAiExtractionService(AiProviderFactory providerFactory, ILogger<ImportAiExtractionService> logger)
    {
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public async Task<ExtractionResult> ExtractStructuredDataAsync(string unstructuredText)
    {
        if (string.IsNullOrWhiteSpace(unstructuredText))
        {
            return new ExtractionResult { Confidence = 0.0 };
        }

        try
        {
            var provider = _providerFactory.GetActiveProvider();
            var prompt = GetExtractionPrompt(unstructuredText);
            var response = await provider.GenerateContentAsync(prompt, requireJson: true);
            if (!string.IsNullOrEmpty(response))
            {
                var cleanJson = CleanJson(response);
                var result = JsonSerializer.Deserialize<ExtractionResult>(cleanJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (result != null)
                {
                    // Ensure FieldConfidences is initialized if missing
                    if (result.FieldConfidences == null)
                    {
                        result.FieldConfidences = new Dictionary<string, double>();
                    }
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract structured data from active AI provider. Using fallback.");
        }

        // Fallback: rule-based/regex parsing
        return RunFallbackExtraction(unstructuredText);
    }

    public async Task<string> ExplainIssueAsync(string issueType, string fieldName, string fieldValue, string issueDetails, string? matchedCustomerDetails)
    {
        try
        {
            var provider = _providerFactory.GetActiveProvider();
            var prompt = $@"Explain the following import issue to a data administrator. Give a clear explanation of why this happened and a helpful, brief suggestion on how to fix it or why it is flagged.

Issue Context:
- Issue Type: {issueType} (can be 'validation', 'duplicate', or 'business_type')
- Field Name: {fieldName}
- Field Value: {fieldValue}
- Technical Error/Details: {issueDetails}
{(string.IsNullOrEmpty(matchedCustomerDetails) ? "" : $"- Matching Database Customer Details: {matchedCustomerDetails}")}

Provide a concise explanation (maximum 2-3 sentences) suitable for a tooltip or small popup. Be friendly, professional, and clear.";

            var explanation = await provider.GenerateContentAsync(prompt);
            if (!string.IsNullOrEmpty(explanation))
            {
                return explanation.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get AI issue explanation. Using fallback.");
        }

        // Fallback explanation
        if (issueType.Equals("duplicate", StringComparison.OrdinalIgnoreCase))
        {
            return $"The value '{fieldValue}' in field '{fieldName}' matches an existing customer record in the database ({issueDetails}). Review and choose whether to skip, import as new, or update the existing record.";
        }
        if (issueType.Equals("business_type", StringComparison.OrdinalIgnoreCase))
        {
            return $"The business type '{fieldValue}' is not registered in the system's category database. You must resolve this to an existing type or register the new business type first.";
        }
        return $"Validation failed for field '{fieldName}' with value '{fieldValue}'. Details: {issueDetails}. Please correct the value and try again.";
    }

    private string GetExtractionPrompt(string text)
    {
        return $@"Extract structured customer information from the following text.

Target fields are:
- name (company name or customer name)
- address (postal address or location)
- phone (main office/company phone number)
- capital (registered capital amount as a number, ignore currency marks like THB or Baht)
- business_type (type of industry or business sector)
- contact_name (contact person name)
- contact_email (contact person email address)
- contact_tel (contact person phone number)
- contact_position (contact person job title/position)

Unstructured Text:
""{text}""

Return a JSON object only. The JSON must match this exact schema:
{{
  ""name"": ""extracted_name_or_null"",
  ""address"": ""extracted_address_or_null"",
  ""phone"": ""extracted_phone_or_null"",
  ""capital"": null_or_number,
  ""business_type"": ""extracted_business_type_or_null"",
  ""contact_name"": ""extracted_contact_name_or_null"",
  ""contact_email"": ""extracted_contact_email_or_null"",
  ""contact_tel"": ""extracted_contact_tel_or_null"",
  ""contact_position"": ""extracted_contact_position_or_null"",
  ""fieldConfidences"": {{
    ""name"": 0.0_to_1.0,
    ""address"": 0.0_to_1.0,
    ""phone"": 0.0_to_1.0,
    ""capital"": 0.0_to_1.0,
    ""business_type"": 0.0_to_1.0,
    ""contact_name"": 0.0_to_1.0,
    ""contact_email"": 0.0_to_1.0,
    ""contact_tel"": 0.0_to_1.0,
    ""contact_position"": 0.0_to_1.0
  }},
  ""confidence"": 0.85
}}
Do not return any explanations, markdown headers, or other text besides the raw JSON.";
    }

    private string CleanJson(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```"))
        {
            var nextNewLine = text.IndexOf('\n');
            var lastBackticks = text.LastIndexOf("```");
            if (nextNewLine > 0 && lastBackticks > nextNewLine)
            {
                text = text.Substring(nextNewLine + 1, lastBackticks - nextNewLine - 1).Trim();
            }
        }
        return text;
    }

    private ExtractionResult RunFallbackExtraction(string text)
    {
        var result = new ExtractionResult();
        int matchesCount = 0;

        var phoneRegex = new System.Text.RegularExpressions.Regex(@"\b(0\d{1,2}-\d{3,4}-\d{3,4}|0\d{8,9})\b");
        var phoneMatch = phoneRegex.Match(text);
        if (phoneMatch.Success)
        {
            result.Phone = phoneMatch.Value;
            result.FieldConfidences["phone"] = 0.85;
            matchesCount++;
        }

        var emailRegex = new System.Text.RegularExpressions.Regex(@"\b[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}\b");
        var emailMatch = emailRegex.Match(text);
        if (emailMatch.Success)
        {
            result.ContactEmail = emailMatch.Value;
            result.FieldConfidences["contact_email"] = 0.85;
            matchesCount++;
        }

        var capitalRegex = new System.Text.RegularExpressions.Regex(@"(?i)(?:capital|cap|cap:|money|registered|ทุน)\s*(?:is)?\s*(\d{1,3}(?:,\d{3})*(?:\.\d+)?|\d+)\b");
        var capMatch = capitalRegex.Match(text);
        if (capMatch.Success && capMatch.Groups.Count > 1)
        {
            var numStr = capMatch.Groups[1].Value.Replace(",", "");
            if (double.TryParse(numStr, out var capVal))
            {
                result.Capital = capVal;
                result.FieldConfidences["capital"] = 0.85;
                matchesCount++;
            }
        }

        result.Confidence = matchesCount > 0 ? Math.Min(0.75, 0.4 + (matchesCount * 0.1)) : 0.3;
        
        // Populate missing confidences with 0.0
        string[] fields = { "name", "address", "phone", "capital", "business_type", "contact_name", "contact_email", "contact_tel", "contact_position" };
        foreach (var field in fields)
        {
            if (!result.FieldConfidences.ContainsKey(field))
            {
                result.FieldConfidences[field] = 0.0;
            }
        }

        return result;
    }
}
