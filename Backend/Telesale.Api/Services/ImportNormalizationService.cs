using System;
using System.Text.RegularExpressions;

namespace Telesale.Api.Services;

public class ImportNormalizationService : IImportNormalizationService
{
    public string? CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        
        // Trim whitespace and remove double spacing
        var cleaned = value.Trim();
        cleaned = Regex.Replace(cleaned, @"\s+", " ");

        // Prevent CSV/Excel Formula Injection
        if (cleaned.StartsWith("=") || 
            cleaned.StartsWith("+") || 
            cleaned.StartsWith("-") || 
            cleaned.StartsWith("@") || 
            cleaned.StartsWith("\t") || 
            cleaned.StartsWith("\r"))
        {
            cleaned = "'" + cleaned;
        }

        return cleaned;
    }

    public string NormalizePhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

        // Remove spaces, dashes, parentheses, plus
        var clean = Regex.Replace(phone, @"[\s\-\(\)\+]+", "");
        
        // If it starts with 66 (Thai country code), replace with 0
        if (clean.StartsWith("66"))
        {
            clean = "0" + clean.Substring(2);
        }
        
        return clean;
    }

    public string NormalizeCapital(string capital)
    {
        if (string.IsNullOrWhiteSpace(capital)) return string.Empty;

        // Strip commas, currency marks like บาท, baht, thb, spaces
        var clean = Regex.Replace(capital, @"(?i)[\s,฿]|บาท|baht|thb", "");
        return clean;
    }
}
