using System.Text.RegularExpressions;
using Telesale.Api.Models;

namespace Telesale.Api.Services;

public sealed record CustomerSearchTerm(string Text, string PhoneText);

public sealed record CustomerSearchMatch(customer Customer, int Rank, string MatchedField);

public sealed record TokenMatch(string Token, string Field, int Rank);

public sealed record MultiTokenSearchTerm(string OriginalQuery, List<CustomerSearchTerm> Tokens);

public sealed record ContactSearchDocument(string? Name, string? Phone, string? Email);

public sealed record CustomerSearchDocument(
    customer Customer,
    string? BusinessType,
    string? SaleName,
    string? TelesaleName,
    IEnumerable<ContactSearchDocument>? Contacts = null,
    IEnumerable<string?>? BookingNumbers = null);

public static class CustomerSearch
{
    private const int FuzzyDistanceLimit = 2;

    public static CustomerSearchTerm? Normalize(string? keyword)
    {
        var text = keyword?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return new CustomerSearchTerm(text, NormalizePhone(text));
    }

    public static CustomerSearchMatch? Rank(
        customer customer,
        CustomerSearchTerm term,
        int? contactEmailRank = null,
        int? contactPhoneRank = null,
        int? bookingNumberRank = null)
    {
        var name = NormalizeText(customer.name);
        var address = NormalizeText(customer.address);
        var code = NormalizeText(customer.code);
        var phone = NormalizePhone(customer.phone);
        var id = customer.id.ToString();

        if (id == term.Text)
        {
            return new CustomerSearchMatch(customer, 0, "Customer ID");
        }

        var codeRank = AccurateTextRank(code, term.Text);
        var phoneRank = AccuratePhoneRank(phone, term.PhoneText);
        var addressRank = AccurateTextRank(address, term.Text);

        if (codeRank.HasValue)
        {
            return new CustomerSearchMatch(customer, codeRank.Value, "Customer code");
        }

        if (phoneRank.HasValue)
        {
            return new CustomerSearchMatch(customer, phoneRank.Value, "Phone");
        }

        if (EqualsTerm(name, term.Text))
        {
            return new CustomerSearchMatch(customer, 0, "Customer name");
        }

        if (StartsWithTerm(name, term.Text))
        {
            return new CustomerSearchMatch(customer, 1, "Customer name");
        }

        if (addressRank.HasValue)
        {
            return new CustomerSearchMatch(customer, addressRank.Value, "Address");
        }

        if (contactEmailRank.HasValue)
        {
            return new CustomerSearchMatch(customer, contactEmailRank.Value, "Email");
        }

        if (contactPhoneRank.HasValue)
        {
            return new CustomerSearchMatch(customer, contactPhoneRank.Value, "Phone");
        }

        if (bookingNumberRank.HasValue)
        {
            return new CustomerSearchMatch(customer, bookingNumberRank.Value, "Booking number");
        }

        if (!string.IsNullOrEmpty(name) && name.Contains(term.Text))
        {
            return new CustomerSearchMatch(customer, 2, "Customer name");
        }

        if (IsFuzzyNameMatch(name, term.Text))
        {
            return new CustomerSearchMatch(customer, 3, "Customer name (fuzzy)");
        }

        return null;
    }

    public static string NormalizeText(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    public static string NormalizePhone(string? value)
    {
        return Regex.Replace(value ?? string.Empty, @"[\s\-\(\)\[\]\{\}\.+#/\\,;:]+", "").ToLowerInvariant();
    }

    public static int? AccurateTextRank(string? value, string term)
    {
        var normalized = NormalizeText(value);
        if (EqualsTerm(normalized, term)) return 0;
        if (StartsWithTerm(normalized, term)) return 1;
        if (!string.IsNullOrEmpty(normalized) && normalized.Contains(term)) return 2;
        return null;
    }

    public static int? AccuratePhoneRank(string? value, string phoneTerm)
    {
        var normalized = NormalizePhone(value);
        if (EqualsTerm(normalized, phoneTerm)) return 0;
        if (StartsWithTerm(normalized, phoneTerm)) return 1;
        if (!string.IsNullOrEmpty(normalized) && normalized.Contains(phoneTerm)) return 2;
        return null;
    }

    private static bool EqualsTerm(string value, string term)
    {
        return !string.IsNullOrEmpty(value) && value == term;
    }

    private static bool StartsWithTerm(string value, string term)
    {
        return !string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(term) && value.StartsWith(term);
    }

    private static bool IsFuzzyNameMatch(string name, string term)
    {
        if (term.Length < 4 || string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var nameParts = SplitTerms(name).ToList();
        var termParts = SplitTerms(term).ToList();

        if (termParts.Count > 1)
        {
            return termParts.All(termPart =>
                termPart.Length < 4 ||
                nameParts.Any(namePart => IsFuzzyPartMatch(namePart, termPart)));
        }

        return nameParts
            .Append(name)
            .Any(part => IsFuzzyPartMatch(part, term));
    }

    private static IEnumerable<string> SplitTerms(string value)
    {
        return value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool IsFuzzyPartMatch(string value, string term)
    {
        return Math.Abs(value.Length - term.Length) <= FuzzyDistanceLimit &&
               LevenshteinDistance(value, term) <= FuzzyDistanceLimit;
    }

    private static int LevenshteinDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    public static MultiTokenSearchTerm? NormalizeMultiToken(string? keyword)
    {
        var trimmed = keyword?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return null;

        // Collapse repeated whitespace
        var normalized = Regex.Replace(trimmed, @"\s+", " ").ToLowerInvariant();
        var tokenStrings = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokenStrings.Length == 0) return null;

        var tokens = tokenStrings.Select(t => new CustomerSearchTerm(t, NormalizePhone(t))).ToList();
        return new MultiTokenSearchTerm(normalized, tokens);
    }

    public static CustomerSearchMatch? RankMultiToken(
        customer customer,
        MultiTokenSearchTerm multiTerm,
        int? contactEmailRank = null,
        int? contactPhoneRank = null,
        int? bookingNumberRank = null)
    {
        // If only 1 token, delegate to existing Rank for backward compat
        if (multiTerm.Tokens.Count == 1)
        {
            return Rank(customer, multiTerm.Tokens[0], contactEmailRank, contactPhoneRank, bookingNumberRank);
        }

        var name = NormalizeText(customer.name);
        var address = NormalizeText(customer.address);
        var code = NormalizeText(customer.code);
        var phone = NormalizePhone(customer.phone);
        var id = customer.id.ToString();

        var tokenMatches = new List<TokenMatch>();

        foreach (var token in multiTerm.Tokens)
        {
            var bestMatch = FindBestFieldMatch(token, id, name, address, code, phone);
            if (bestMatch != null)
            {
                tokenMatches.Add(bestMatch);
            }
        }

        if (tokenMatches.Count < multiTerm.Tokens.Count) return null;

        int totalTokens = multiTerm.Tokens.Count;
        int matchedCount = tokenMatches.Count;
        int sumRanks = tokenMatches.Sum(m => m.Rank);

        int compositeRank = (totalTokens - matchedCount) * 1000 + sumRanks;

        // Build matched fields label - unique field names
        var matchedFields = tokenMatches
            .Select(m => m.Field)
            .Distinct()
            .ToList();
        string matchedField = string.Join(", ", matchedFields);

        return new CustomerSearchMatch(customer, compositeRank, matchedField);
    }

    public static CustomerSearchMatch? RankDocument(CustomerSearchDocument document, MultiTokenSearchTerm multiTerm)
    {
        if (multiTerm.Tokens.Count == 1)
        {
            var token = multiTerm.Tokens[0];
            var best = FindBestDocumentTokenMatch(document, token);
            return best == null ? null : new CustomerSearchMatch(document.Customer, best.Rank, best.Field);
        }

        var tokenMatches = new List<TokenMatch>();
        foreach (var token in multiTerm.Tokens)
        {
            var bestMatch = FindBestDocumentTokenMatch(document, token);
            if (bestMatch != null)
            {
                tokenMatches.Add(bestMatch);
            }
        }

        if (tokenMatches.Count < multiTerm.Tokens.Count) return null;

        var compositeRank = (multiTerm.Tokens.Count - tokenMatches.Count) * 1000 + tokenMatches.Sum(m => m.Rank);
        var matchedField = string.Join(", ", tokenMatches.Select(m => m.Field).Distinct());
        return new CustomerSearchMatch(document.Customer, compositeRank, matchedField);
    }

    private static TokenMatch? FindBestFieldMatch(
        CustomerSearchTerm token, string id, string name, string address, string code, string phone)
    {
        var text = token.Text;
        var phoneTerm = token.PhoneText;

        // ID exact match
        if (id == text)
            return new TokenMatch(text, "Customer ID", 0);

        // Code match
        var codeRank = AccurateTextRank(code, text);
        if (codeRank.HasValue)
            return new TokenMatch(text, "Customer code", codeRank.Value);

        // Phone match
        var phoneRank = AccuratePhoneRank(phone, phoneTerm);
        if (phoneRank.HasValue)
            return new TokenMatch(text, "Phone", phoneRank.Value);

        // Name exact
        if (EqualsTerm(name, text))
            return new TokenMatch(text, "Customer name", 0);

        // Name starts with
        if (StartsWithTerm(name, text))
            return new TokenMatch(text, "Customer name", 1);

        // Name contains
        if (!string.IsNullOrEmpty(name) && name.Contains(text))
            return new TokenMatch(text, "Customer name", 2);

        // Address match
        var addressRank = AccurateTextRank(address, text);
        if (addressRank.HasValue)
            return new TokenMatch(text, "Address", addressRank.Value);

        // Name fuzzy
        if (IsFuzzyNameMatch(name, text))
            return new TokenMatch(text, "Customer name (fuzzy)", 3);

        return null;
    }

    private static TokenMatch? FindBestDocumentTokenMatch(CustomerSearchDocument document, CustomerSearchTerm token)
    {
        var customer = document.Customer;
        var text = token.Text;
        var phoneTerm = token.PhoneText;
        var id = customer.id.ToString();

        var candidates = new List<TokenMatch>();

        if (id == text)
        {
            candidates.Add(new TokenMatch(text, "Customer ID", 0));
        }

        AddAccurateTextMatch(candidates, text, "Customer code", customer.code);
        AddAccuratePhoneMatch(candidates, text, phoneTerm, "Phone", customer.phone);
        AddAccurateTextMatch(candidates, text, "Customer name", customer.name, fuzzy: true);
        AddAccurateTextMatch(candidates, text, "Address", customer.address);
        AddAccurateTextMatch(candidates, text, "Business type", document.BusinessType, fuzzy: true);
        AddAccurateTextMatch(candidates, text, "Sale", document.SaleName);
        AddAccurateTextMatch(candidates, text, "Tele sale", document.TelesaleName);

        foreach (var contact in document.Contacts ?? Enumerable.Empty<ContactSearchDocument>())
        {
            AddAccurateTextMatch(candidates, text, "Contact name", contact.Name, fuzzy: true);
            AddAccuratePhoneMatch(candidates, text, phoneTerm, "Contact phone", contact.Phone);
            AddAccurateTextMatch(candidates, text, "Contact email", contact.Email);
        }

        foreach (var bookingNumber in document.BookingNumbers ?? Enumerable.Empty<string?>())
        {
            AddAccurateTextMatch(candidates, text, "Booking number", bookingNumber);
        }

        return candidates.OrderBy(c => c.Rank).FirstOrDefault();
    }

    private static void AddAccurateTextMatch(
        List<TokenMatch> candidates,
        string token,
        string field,
        string? value,
        bool fuzzy = false)
    {
        var rank = AccurateTextRank(value, token);
        if (rank.HasValue)
        {
            candidates.Add(new TokenMatch(token, field, rank.Value));
        }
        else if (fuzzy && IsFuzzyNameMatch(NormalizeText(value), token))
        {
            candidates.Add(new TokenMatch(token, $"{field} (fuzzy)", 3));
        }
    }

    private static void AddAccuratePhoneMatch(
        List<TokenMatch> candidates,
        string token,
        string phoneTerm,
        string field,
        string? value)
    {
        var rank = AccuratePhoneRank(value, phoneTerm);
        if (rank.HasValue)
        {
            candidates.Add(new TokenMatch(token, field, rank.Value));
        }
    }
}
