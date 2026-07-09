using System.Text.RegularExpressions;
using ExcelDataReader;

namespace Telesale.Api.Services;

public class LocalGovernmentImportParserService : ILocalGovernmentImportParserService
{
    private const int CustomerAddressMaxLength = 255;

    private static readonly string[] RequiredHeaders =
    {
        "ลำดับ",
        "จังหวัด",
        "ประเภท",
        "ชื่อหน่วยงาน",
        "เบอร์หน่วยงาน",
        "ชื่อ - สกุล (นายก)",
        "เบอร์ (นายก)",
        "ชื่อ - สกุล (รองนายก)",
        "เบอร์ (รองนายก)",
        "ผอ.การศึกษา",
        "เบอร์ (ผอ.การศึกษา)",
        "จำนวนโรงเรียนในสังกัด",
        "หมายเหตุ"
    };

    public async Task<LocalGovernmentImportPreviewSummary> ParsePreviewFileAsync(
        Stream stream,
        string extension,
        ISet<string> existingNormalizedOrganizationNames,
        CancellationToken cancellationToken)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        using var reader = extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? ExcelReaderFactory.CreateCsvReader(stream)
            : ExcelReaderFactory.CreateOpenXmlReader(stream);

        var headers = new List<string>();
        var dataRows = new List<IReadOnlyList<string?>>();

        if (reader.Read())
        {
            for (var i = 0; i < reader.FieldCount; i++)
            {
                headers.Add(CleanText(reader.GetValue(i)?.ToString()) ?? $"Column_{i + 1}");
            }
        }

        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = new List<string?>();
            for (var i = 0; i < headers.Count; i++)
            {
                row.Add(i < reader.FieldCount ? CleanText(reader.GetValue(i)?.ToString()) : null);
            }
            dataRows.Add(row);
        }

        return ParsePreview(headers, dataRows, existingNormalizedOrganizationNames);
    }

    public LocalGovernmentImportPreviewSummary ParsePreview(
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string?>> rows,
        ISet<string> existingNormalizedOrganizationNames)
    {
        var summary = new LocalGovernmentImportPreviewSummary();
        var indexes = BuildHeaderIndex(headers);

        foreach (var requiredHeader in RequiredHeaders)
        {
            if (!indexes.ContainsKey(requiredHeader))
            {
                summary.Errors.Add(new LocalGovernmentImportIssue
                {
                    Field = requiredHeader,
                    Message = $"Missing required header '{requiredHeader}'."
                });
            }
        }

        if (summary.Errors.Count > 0)
        {
            return summary;
        }

        var rowNumber = 1;
        foreach (var values in rows)
        {
            rowNumber++;
            var raw = BuildRawRow(rowNumber, values, indexes);
            var preview = BuildRowPreview(raw, existingNormalizedOrganizationNames);
            summary.Rows.Add(preview);
        }

        RecalculateSummary(summary);
        return summary;
    }

    public string? NormalizeOrganizationName(string? value)
    {
        return CleanText(value);
    }

    private static Dictionary<string, int> BuildHeaderIndex(IReadOnlyList<string> headers)
    {
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < headers.Count; i++)
        {
            var header = CleanText(headers[i]);
            if (!string.IsNullOrWhiteSpace(header) && !indexes.ContainsKey(header))
            {
                indexes[header] = i;
            }
        }

        return indexes;
    }

    private static LocalGovernmentRawImportRow BuildRawRow(
        int rowNumber,
        IReadOnlyList<string?> values,
        Dictionary<string, int> indexes)
    {
        string? Get(string header)
        {
            var index = indexes[header];
            return index < values.Count ? CleanText(values[index]) : null;
        }

        return new LocalGovernmentRawImportRow
        {
            RowNumber = rowNumber,
            Sequence = Get("ลำดับ"),
            Province = Get("จังหวัด"),
            Type = Get("ประเภท"),
            OrganizationName = Get("ชื่อหน่วยงาน"),
            OfficePhone = Get("เบอร์หน่วยงาน"),
            MayorName = Get("ชื่อ - สกุล (นายก)"),
            MayorPhone = Get("เบอร์ (นายก)"),
            DeputyMayorName = Get("ชื่อ - สกุล (รองนายก)"),
            DeputyMayorPhone = Get("เบอร์ (รองนายก)"),
            EducationDirectorName = Get("ผอ.การศึกษา"),
            EducationDirectorPhone = Get("เบอร์ (ผอ.การศึกษา)"),
            SchoolCount = Get("จำนวนโรงเรียนในสังกัด"),
            Note = Get("หมายเหตุ")
        };
    }

    private LocalGovernmentImportRowPreview BuildRowPreview(
        LocalGovernmentRawImportRow raw,
        ISet<string> existingNormalizedOrganizationNames)
    {
        var normalizedName = NormalizeOrganizationName(raw.OrganizationName);
        var row = new LocalGovernmentImportRowPreview
        {
            RowNumber = raw.RowNumber,
            OrganizationName = raw.OrganizationName,
            NormalizedOrganizationName = normalizedName,
            IsDuplicate = normalizedName != null && existingNormalizedOrganizationNames.Contains(normalizedName)
        };

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            row.Errors.Add(new LocalGovernmentImportIssue
            {
                RowNumber = raw.RowNumber,
                Field = "ชื่อหน่วยงาน",
                Message = "ชื่อหน่วยงาน is required."
            });
            return row;
        }

        var address = ComposeAddress(raw, row.Warnings);
        row.CustomerPreview = new LocalGovernmentCustomerPreview
        {
            Name = raw.OrganizationName,
            Phone = raw.OfficePhone,
            Address = address
        };

        row.DetailPreviews.AddRange(ParseIndexedContacts(
            raw.MayorName,
            raw.MayorPhone,
            "นายก",
            "ชื่อ - สกุล (นายก)",
            "เบอร์ (นายก)",
            raw.OfficePhone,
            raw.RowNumber,
            row.Warnings));

        var deputyPipeContacts = ParseDeputyPipeContacts(raw.DeputyMayorPhone, raw.OfficePhone);
        if (deputyPipeContacts.Count > 0)
        {
            row.DetailPreviews.AddRange(deputyPipeContacts);
        }
        else
        {
            row.DetailPreviews.AddRange(ParseIndexedContacts(
                raw.DeputyMayorName,
                raw.DeputyMayorPhone,
                "รองนายก",
                "ชื่อ - สกุล (รองนายก)",
                "เบอร์ (รองนายก)",
                raw.OfficePhone,
                raw.RowNumber,
                row.Warnings));
        }

        row.DetailPreviews.AddRange(ParseIndexedContacts(
            raw.EducationDirectorName,
            raw.EducationDirectorPhone,
            "ผอ.การศึกษา",
            "ผอ.การศึกษา",
            "เบอร์ (ผอ.การศึกษา)",
            raw.OfficePhone,
            raw.RowNumber,
            row.Warnings));

        return row;
    }

    private static string? ComposeAddress(
        LocalGovernmentRawImportRow raw,
        List<LocalGovernmentImportIssue> warnings)
    {
        var parts = new List<string>();
        AddPart(parts, "จังหวัด", raw.Province);
        AddPart(parts, "ประเภท", raw.Type);
        AddPart(parts, "จำนวนโรงเรียนในสังกัด", raw.SchoolCount);
        AddPart(parts, "หมายเหตุ", raw.Note);

        if (parts.Count == 0)
        {
            return null;
        }

        var address = string.Join(Environment.NewLine, parts);
        if (address.Length <= CustomerAddressMaxLength)
        {
            return address;
        }

        warnings.Add(new LocalGovernmentImportIssue
        {
            RowNumber = raw.RowNumber,
            Field = "address",
            Message = $"Composed address exceeds {CustomerAddressMaxLength} characters and was trimmed."
        });
        return address[..CustomerAddressMaxLength];
    }

    private static void AddPart(List<string> parts, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{label}: {value}");
        }
    }

    private static List<LocalGovernmentDetailPreview> ParseIndexedContacts(
        string? namesValue,
        string? phonesValue,
        string position,
        string nameField,
        string phoneField,
        string? officePhone,
        int rowNumber,
        List<LocalGovernmentImportIssue> warnings)
    {
        var details = new List<LocalGovernmentDetailPreview>();
        var names = SplitCommaValues(namesValue);
        var phones = SplitCommaValues(phonesValue);
        var count = Math.Max(names.Count, phones.Count);

        if (names.Count > 0 && phones.Count > 0 && names.Count != phones.Count)
        {
            warnings.Add(new LocalGovernmentImportIssue
            {
                RowNumber = rowNumber,
                Field = phoneField,
                Message = $"{nameField} and {phoneField} counts do not match; contacts were mapped by index."
            });
        }

        for (var i = 0; i < count; i++)
        {
            var name = i < names.Count ? names[i] : null;
            var phone = i < phones.Count ? phones[i] : null;

            if (!string.IsNullOrWhiteSpace(name))
            {
                details.Add(new LocalGovernmentDetailPreview
                {
                    ContactName = name,
                    ContactTel = phone,
                    ContactPosition = position,
                    ContactTelOffice = officePhone
                });
                continue;
            }

            if (!string.IsNullOrWhiteSpace(phone))
            {
                warnings.Add(new LocalGovernmentImportIssue
                {
                    RowNumber = rowNumber,
                    Field = phoneField,
                    Message = $"{phoneField} has a phone value without a matching {nameField}; contact was skipped."
                });
            }
        }

        return details;
    }

    private static List<LocalGovernmentDetailPreview> ParseDeputyPipeContacts(
        string? deputyPhone,
        string? officePhone)
    {
        var details = new List<LocalGovernmentDetailPreview>();
        if (string.IsNullOrWhiteSpace(deputyPhone) || !deputyPhone.Contains('|'))
        {
            return details;
        }

        foreach (var segment in deputyPhone.Split('|'))
        {
            var trimmed = CleanText(segment);
            if (string.IsNullOrWhiteSpace(trimmed) || !trimmed.EndsWith(')'))
            {
                continue;
            }

            var openParen = trimmed.LastIndexOf('(');
            if (openParen <= 0 || openParen >= trimmed.Length - 1)
            {
                continue;
            }

            var phone = CleanText(trimmed[..openParen]);
            var name = CleanText(trimmed[(openParen + 1)..^1]);
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(phone))
            {
                continue;
            }

            details.Add(new LocalGovernmentDetailPreview
            {
                ContactName = name,
                ContactTel = phone,
                ContactPosition = "รองนายก",
                ContactTelOffice = officePhone
            });
        }

        return details;
    }

    private static List<string?> SplitCommaValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string?>();
        }

        return value
            .Split(',', StringSplitOptions.None)
            .Select(CleanText)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    private static void RecalculateSummary(LocalGovernmentImportPreviewSummary summary)
    {
        summary.TotalRows = summary.Rows.Count;
        summary.DuplicateRows = summary.Rows.Count(row => row.IsDuplicate);
        summary.ErrorRows = summary.Rows.Count(row => row.Errors.Count > 0);
        summary.ValidRows = summary.Rows.Count(row => row.Errors.Count == 0 && !row.IsDuplicate);
        summary.EstimatedCustomersToInsert = summary.ValidRows;
        summary.EstimatedDetailsToInsert = summary.Rows
            .Where(row => row.Errors.Count == 0 && !row.IsDuplicate)
            .Sum(row => row.DetailPreviews.Count);

        summary.Warnings = summary.Rows.SelectMany(row => row.Warnings).ToList();
        summary.Errors.AddRange(summary.Rows.SelectMany(row => row.Errors));
    }

    private static string? CleanText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Regex.Replace(value.Trim(), @"\s+", " ");
    }
}
