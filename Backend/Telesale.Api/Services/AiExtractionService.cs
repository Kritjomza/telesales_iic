using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Telesale.Api.Services;

public class AiExtractionService : IAiExtractionService
{
    private readonly AiProviderFactory _providerFactory;
    private readonly ILogger<AiExtractionService> _logger;

    public AiExtractionService(AiProviderFactory providerFactory, ILogger<AiExtractionService> logger)
    {
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public async Task<ColumnMappingResult> SuggestMappingsAsync(List<string> columns)
    {
        if (columns == null || columns.Count == 0)
        {
            return new ColumnMappingResult();
        }

        try
        {
            var provider = _providerFactory.GetActiveProvider();
            var prompt = GetMappingPrompt(columns);
            var response = await provider.GenerateContentAsync(prompt, requireJson: true);
            if (!string.IsNullOrEmpty(response))
            {
                var cleanJson = CleanJson(response);
                var result = JsonSerializer.Deserialize<ColumnMappingResult>(cleanJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (result != null)
                {
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch mappings suggestion from active AI provider. Using fallback.");
        }

        // Fallback: rule-based matching
        return RunFallbackMapping(columns);
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

    private string GetMappingPrompt(List<string> columns)
    {
        var colsJson = JsonSerializer.Serialize(columns);
        return $@"Suggest mappings from the following list of uploaded columns to the target customer database fields.

Target fields are:
- name (Company or Customer name)
- address (Location/Street/City details)
- phone (Main contact phone number)
- capital (Registered Capital amount)
- business_type (Type of industry or business)
- contact_name (Contact person name)
- contact_email (Contact person email)
- contact_tel (Contact person tel)
- contact_position (Contact person job title)

Uploaded columns: {colsJson}

Return a JSON object only. The JSON must match this exact schema:
{{
  ""mappings"": [
    {{ ""column"": ""Uploaded Column Name"", ""targetField"": ""target_field_name"" }}
  ],
  ""confidence"": 0.95
}}
Do not return any explanations, markdown headers, or other text besides the raw JSON.";
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

    private ColumnMappingResult RunFallbackMapping(List<string> columns)
    {
        var mappings = new List<ColumnMappingItem>();
        var fieldKeywords = new Dictionary<string, string[]>
        {
            { "name", new[] { "name", "company", "customer", "customer name", "company name", "client", "firm", "business name" } },
            { "address", new[] { "address", "location", "street", "city", "province", "road", "bangkok", "zip", "post" } },
            { "phone", new[] { "phone", "tel", "mobile", "contact number", "telephone" } },
            { "capital", new[] { "capital", "registered capital", "registered_capital", "money", "funds" } },
            { "business_type", new[] { "business type", "business_type", "type", "industry", "sector", "category" } },
            { "contact_name", new[] { "contact name", "contact_name", "pic", "person", "representative" } },
            { "contact_email", new[] { "contact email", "contact_email", "email", "mail" } },
            { "contact_tel", new[] { "contact tel", "contact_tel", "contact phone", "contact_phone", "pic phone" } },
            { "contact_position", new[] { "contact position", "contact_position", "position", "role", "title" } }
        };

        foreach (var col in columns)
        {
            var colLower = col.Trim().ToLowerInvariant();
            string? matchedField = null;

            foreach (var kvp in fieldKeywords)
            {
                foreach (var kw in kvp.Value)
                {
                    if (colLower == kw || colLower.Contains(kw))
                    {
                        matchedField = kvp.Key;
                        break;
                    }
                }
                if (matchedField != null) break;
            }

            if (matchedField != null)
            {
                mappings.Add(new ColumnMappingItem { Column = col, TargetField = matchedField });
            }
        }

        return new ColumnMappingResult
        {
            Mappings = mappings,
            Confidence = 0.8
        };
    }

    private ExtractionResult RunFallbackExtraction(string text)
    {
        var result = new ExtractionResult();
        int matchesCount = 0;

        var phoneRegex = new Regex(@"\b(0\d{1,2}-\d{3,4}-\d{3,4}|0\d{8,9})\b");
        var phoneMatch = phoneRegex.Match(text);
        if (phoneMatch.Success)
        {
            result.Phone = phoneMatch.Value;
            matchesCount++;
        }

        var emailRegex = new Regex(@"\b[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}\b");
        var emailMatch = emailRegex.Match(text);
        if (emailMatch.Success)
        {
            result.ContactEmail = emailMatch.Value;
            matchesCount++;
        }

        var capitalRegex = new Regex(@"(?i)(?:capital|cap|cap:|money|registered)\s*(?:is)?\s*(\d{1,3}(?:,\d{3})*(?:\.\d+)?|\d+)\b");
        var capMatch = capitalRegex.Match(text);
        if (capMatch.Success && capMatch.Groups.Count > 1)
        {
            var numStr = capMatch.Groups[1].Value.Replace(",", "");
            if (double.TryParse(numStr, out var capVal))
            {
                result.Capital = capVal;
                matchesCount++;
            }
        }
        else
        {
            var genericCapRegex = new Regex(@"\b(\d{1,3}(?:,\d{3}){2,}(?:\.\d+)?|\d{6,8})\b");
            var genMatch = genericCapRegex.Match(text);
            if (genMatch.Success)
            {
                var numStr = genMatch.Value.Replace(",", "");
                if (double.TryParse(numStr, out var capVal))
                {
                    result.Capital = capVal;
                    matchesCount++;
                }
            }
        }

        var addressRegex = new Regex(@"\b(\d+/\d+|\d+)\s+[^,;\n]+(?:road|rd|soi|street|st|bangkok|bkk|thailand)\b", RegexOptions.IgnoreCase);
        var addrMatch = addressRegex.Match(text);
        if (addrMatch.Success)
        {
            result.Address = addrMatch.Value;
            matchesCount++;
        }
        else
        {
            var addressKeywords = new[] { "silom", "bangkok", "bkk", "sukhumvit", "road", "rd", "soi" };
            foreach (var kw in addressKeywords)
            {
                var idx = text.ToLowerInvariant().IndexOf(kw);
                if (idx >= 0)
                {
                    var start = Math.Max(0, idx - 15);
                    var len = Math.Min(text.Length - start, 50);
                    result.Address = text.Substring(start, len).Trim();
                    matchesCount++;
                    break;
                }
            }
        }

        var companyNameRegex = new Regex(@"\b[A-Za-z0-9\s\-]+(?i)(?:Co\.,\s*Ltd\b|Co\.\s*Ltd\b|Company\s+Limited\b|Ltd\b)");
        var nameMatch = companyNameRegex.Match(text);
        if (nameMatch.Success)
        {
            result.Name = nameMatch.Value.Trim();
            matchesCount++;
        }
        else if (text.Length < 30)
        {
            result.Name = text.Trim();
        }

        result.Confidence = matchesCount > 0 ? Math.Min(0.75, 0.4 + (matchesCount * 0.1)) : 0.3;
        return result;
    }
}
