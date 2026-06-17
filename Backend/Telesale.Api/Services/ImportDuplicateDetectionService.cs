using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;
using FuzzySharp;

namespace Telesale.Api.Services;

public class ImportDuplicateDetectionService : IImportDuplicateDetectionService
{
    private readonly TelesaleDbContext _db;

    public ImportDuplicateDetectionService(TelesaleDbContext db)
    {
        _db = db;
    }

    public async Task<DuplicateMatchResult> DetectDuplicateAsync(string? name, string? phone, string? code)
    {
        var result = new DuplicateMatchResult();

        // If all input fields are empty, it's unique
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(code))
        {
            return result;
        }

        // Normalize inputs
        var inputName = name?.Trim();
        var inputPhoneNormalized = !string.IsNullOrWhiteSpace(phone) ? NormalizePhoneNumber(phone) : null;
        var inputCode = code?.Trim();

        // Fetch active customers from the database for matching (selecting only required columns for memory optimization)
        var dbCustomers = await _db.customers
            .AsNoTracking()
            .Where(c => c.is_active != false)
            .Select(c => new 
            { 
                c.id, 
                c.name, 
                c.phone, 
                c.code 
            })
            .ToListAsync();

        int bestScore = 0;
        string bestReason = "Unique";
        string bestStatus = "unique";
        string? matchedName = null;
        string? matchedCode = null;
        uint? matchedId = null;

        foreach (var dbCust in dbCustomers)
        {
            int currentScore = 0;
            string currentReason = string.Empty;

            // 1. Exact Match Check: Code
            if (!string.IsNullOrEmpty(inputCode) && !string.IsNullOrEmpty(dbCust.code) &&
                string.Equals(inputCode, dbCust.code.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                currentScore = 100;
                currentReason = "Exact customer code match";
            }

            // 2. Exact Match Check: Phone
            if (currentScore < 100 && !string.IsNullOrEmpty(inputPhoneNormalized) && !string.IsNullOrEmpty(dbCust.phone))
            {
                var dbPhoneNormalized = NormalizePhoneNumber(dbCust.phone);
                if (string.Equals(inputPhoneNormalized, dbPhoneNormalized, StringComparison.OrdinalIgnoreCase))
                {
                    currentScore = 100;
                    currentReason = "Exact phone number match";
                }
            }

            // 3. Fuzzy Match Check: Name
            if (currentScore < 100 && !string.IsNullOrEmpty(inputName) && !string.IsNullOrEmpty(dbCust.name))
            {
                int levScore = CalculateLevenshteinSimilarity(inputName, dbCust.name);
                int fuzzyScore = Fuzz.WeightedRatio(inputName, dbCust.name);
                int nameScore = Math.Max(levScore, fuzzyScore);

                if (nameScore > currentScore)
                {
                    currentScore = nameScore;
                    currentReason = $"Fuzzy name match ({nameScore}%)";
                }
            }

            // Keep the match with the highest score
            if (currentScore > bestScore)
            {
                bestScore = currentScore;
                bestReason = currentReason;
                matchedName = dbCust.name;
                matchedCode = dbCust.code;
                matchedId = dbCust.id;

                if (bestScore >= 95)
                {
                    bestStatus = "duplicate";
                }
                else if (bestScore >= 85)
                {
                    bestStatus = "warning";
                }
                else
                {
                    bestStatus = "unique";
                }
            }
        }

        // If the best score is below 85, treat it as unique
        if (bestScore >= 85)
        {
            result.Status = bestStatus;
            result.Score = bestScore;
            result.Reason = bestReason;
            result.MatchedCustomerName = matchedName;
            result.MatchedCustomerCode = matchedCode;
            result.MatchedCustomerId = matchedId;
        }

        return result;
    }

    private string NormalizePhoneNumber(string phone)
    {
        // Strip spaces, dashes, parentheses, plus
        var clean = Regex.Replace(phone, @"[\s\-\(\)\+]+", "");
        if (clean.StartsWith("66"))
        {
            clean = "0" + clean.Substring(2);
        }
        return clean;
    }

    private int CalculateLevenshteinSimilarity(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 100 : 0;
        if (string.IsNullOrEmpty(t)) return 0;

        s = s.ToLowerInvariant();
        t = t.ToLowerInvariant();

        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        double maxLength = Math.Max(n, m);
        return (int)Math.Round((1.0 - (d[n, m] / maxLength)) * 100);
    }
}
