using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Telesale.Api.Services;

public class ImportColumnMappingService : IImportColumnMappingService
{
    private readonly IAiExtractionService _aiExtractionService;

    public ImportColumnMappingService(IAiExtractionService aiExtractionService)
    {
        _aiExtractionService = aiExtractionService;
    }

    public async Task<SuggestedMappingResult> SuggestMappingsAsync(List<string> columns)
    {
        var result = new SuggestedMappingResult();
        if (columns == null || columns.Count == 0) return result;

        // Try getting AI mappings first
        ColumnMappingResult? aiMapping = null;
        try
        {
            aiMapping = await _aiExtractionService.SuggestMappingsAsync(columns);
        }
        catch
        {
            // Fallback to rules if AI fails
        }

        var fieldKeywords = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "name", new[] { "ชื่อบริษัท", "ชื่อลูกค้า", "ชื่อ", "name", "company", "customer", "customer name", "company name", "client", "firm", "business name" } },
            { "unstructured_company_info", new[] { "ข้อมูลบริษัท", "ข้อมูลดิบ", "รายละเอียดบริษัท", "unstructured", "raw info", "unstructured_company_info", "unstructured company info", "company info", "raw_info" } },
            { "address", new[] { "ที่อยู่", "ที่ตั้ง", "address", "location", "street", "city", "province", "road" } },
            { "phone", new[] { "เบอร์โทร", "เบอร์โทรศัพท์", "เบอร์บริษัท", "เบอร์ติดต่อ", "phone", "tel", "mobile", "telephone" } },
            { "capital", new[] { "ทุนจดทะเบียน", "ทุน", "capital", "registered capital", "registered_capital" } },
            { "business_type", new[] { "ประเภทธุรกิจ", "ธุรกิจ", "business type", "business_type", "type", "industry", "sector", "category" } },
            { "contact_name", new[] { "ผู้ติดต่อ", "ชื่อผู้ติดต่อ", "contact name", "contact_name", "pic", "representative" } },
            { "contact_email", new[] { "อีเมล", "อีเมลผู้ติดต่อ", "contact email", "contact_email", "email", "mail" } },
            { "contact_tel", new[] { "เบอร์ผู้ติดต่อ", "เบอร์โทรผู้ติดต่อ", "contact tel", "contact_tel", "contact phone", "contact_phone", "pic phone", "pic tel" } },
            { "contact_position", new[] { "ตำแหน่ง", "ตำแหน่งผู้ติดต่อ", "contact position", "contact_position", "position", "role", "title" } }
        };

        foreach (var col in columns)
        {
            var colLower = col.Trim().ToLowerInvariant();
            string? matchedField = null;
            double confidence = 0.0;

            // 1. Rule-based exact keyword match
            foreach (var kvp in fieldKeywords)
            {
                foreach (var keyword in kvp.Value)
                {
                    if (string.Equals(colLower, keyword.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                    {
                        matchedField = kvp.Key;
                        confidence = 0.95; // Exact match keyword
                        break;
                    }
                }
                if (matchedField != null) break;
            }

            // 2. Rule-based contains keyword match
            if (matchedField == null)
            {
                foreach (var kvp in fieldKeywords)
                {
                    foreach (var keyword in kvp.Value)
                    {
                        if (colLower.Contains(keyword.ToLowerInvariant()) || keyword.ToLowerInvariant().Contains(colLower))
                        {
                            matchedField = kvp.Key;
                            confidence = 0.80; // Fuzzy/partial keyword match
                            break;
                        }
                    }
                    if (matchedField != null) break;
                }
            }

            // 3. Fallback to AI mapping suggestion if rule-based confidence is low/none
            if (matchedField == null && aiMapping?.Mappings != null)
            {
                var aiItem = aiMapping.Mappings.Find(m => string.Equals(m.Column, col, StringComparison.OrdinalIgnoreCase));
                if (aiItem != null && !string.IsNullOrEmpty(aiItem.TargetField))
                {
                    matchedField = aiItem.TargetField;
                    confidence = aiMapping.Confidence;
                }
            }

            // If a match is found, classify it based on confidence
            if (matchedField != null)
            {
                var status = "manual_required";
                if (confidence >= 0.90) status = "auto_accepted";
                else if (confidence >= 0.70) status = "review_required";

                result.Mappings.Add(new SuggestedMappingItem
                {
                    Column = col,
                    TargetField = matchedField,
                    Confidence = confidence,
                    Status = status
                });
            }
        }

        // Overall mapping confidence is average of matched columns
        if (result.Mappings.Count > 0)
        {
            double sum = 0.0;
            foreach (var m in result.Mappings) sum += m.Confidence;
            result.Confidence = sum / result.Mappings.Count;
        }
        else
        {
            result.Confidence = 0.0;
        }

        return result;
    }
}
