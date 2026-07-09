using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;
using Telesale.Api.Models;

namespace Telesale.Api.Services;

public class LocalGovernmentImportConfirmService : ILocalGovernmentImportConfirmService
{
    private readonly TelesaleDbContext _db;
    private readonly ILocalGovernmentImportParserService _parser;

    public LocalGovernmentImportConfirmService(
        TelesaleDbContext db,
        ILocalGovernmentImportParserService parser)
    {
        _db = db;
        _parser = parser;
    }

    public async Task<LocalGovernmentImportConfirmResult> ConfirmFileAsync(
        Stream stream,
        string extension,
        uint userId,
        CancellationToken cancellationToken)
    {
        var existingNames = await LoadExistingNormalizedOrganizationNamesAsync(cancellationToken);
        var preview = await _parser.ParsePreviewFileAsync(stream, extension, existingNames, cancellationToken);
        return await ConfirmPreviewAsync(preview, existingNames, userId, cancellationToken);
    }

    public async Task<LocalGovernmentImportConfirmResult> ConfirmAsync(
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string?>> rows,
        uint userId,
        CancellationToken cancellationToken)
    {
        var existingNames = await LoadExistingNormalizedOrganizationNamesAsync(cancellationToken);
        var preview = _parser.ParsePreview(headers, rows, existingNames);
        return await ConfirmPreviewAsync(preview, existingNames, userId, cancellationToken);
    }

    private async Task<LocalGovernmentImportConfirmResult> ConfirmPreviewAsync(
        LocalGovernmentImportPreviewSummary preview,
        HashSet<string> knownNormalizedNames,
        uint userId,
        CancellationToken cancellationToken)
    {
        var result = new LocalGovernmentImportConfirmResult
        {
            TotalRows = preview.TotalRows
        };

        result.Warnings.AddRange(preview.Warnings);

        if (preview.Rows.Count == 0 && preview.Errors.Count > 0)
        {
            result.Errors.AddRange(preview.Errors);
            return result;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var row in preview.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rowResult = new LocalGovernmentImportRowResult
                {
                    RowNumber = row.RowNumber,
                    OrganizationName = row.OrganizationName,
                    NormalizedOrganizationName = row.NormalizedOrganizationName,
                    Warnings = row.Warnings.ToList(),
                    Errors = row.Errors.ToList()
                };

                if (row.Errors.Count > 0 || row.CustomerPreview == null)
                {
                    rowResult.Status = "error_skipped";
                    result.ErrorRows++;
                    result.Errors.AddRange(row.Errors);
                    result.Rows.Add(rowResult);
                    continue;
                }

                if (row.NormalizedOrganizationName != null && knownNormalizedNames.Contains(row.NormalizedOrganizationName))
                {
                    rowResult.Status = "duplicate_skipped";
                    result.SkippedDuplicates++;
                    result.Rows.Add(rowResult);
                    continue;
                }

                var now = DateTime.UtcNow;
                var customer = new customer
                {
                    name = row.CustomerPreview.Name,
                    phone = row.CustomerPreview.Phone,
                    address = row.CustomerPreview.Address,
                    status = row.CustomerPreview.Status,
                    create_type = row.CustomerPreview.CreateType,
                    is_active = row.CustomerPreview.IsActive,
                    owner_id = (int)userId,
                    created_at = now,
                    updated_at = now
                };

                _db.customers.Add(customer);
                await _db.SaveChangesAsync(cancellationToken);

                foreach (var detailPreview in row.DetailPreviews)
                {
                    _db.details.Add(new detail
                    {
                        cust_id = customer.id,
                        contact_name = detailPreview.ContactName,
                        contact_tel = detailPreview.ContactTel,
                        contact_position = detailPreview.ContactPosition,
                        contact_tel_office = detailPreview.ContactTelOffice,
                        is_active = detailPreview.IsActive,
                        created_at = now,
                        updated_at = now
                    });
                }

                await _db.SaveChangesAsync(cancellationToken);

                rowResult.Status = "inserted";
                rowResult.InsertedCustomerId = customer.id;
                rowResult.InsertedDetailCount = row.DetailPreviews.Count;
                result.InsertedCustomers++;
                result.InsertedDetails += row.DetailPreviews.Count;
                result.Rows.Add(rowResult);

                if (row.NormalizedOrganizationName != null)
                {
                    knownNormalizedNames.Add(row.NormalizedOrganizationName);
                }
            }

            result.Warnings = result.Rows.SelectMany(row => row.Warnings).ToList();
            result.Errors = result.Rows.SelectMany(row => row.Errors).ToList();

            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<HashSet<string>> LoadExistingNormalizedOrganizationNamesAsync(CancellationToken cancellationToken)
    {
        var names = await _db.customers
            .AsNoTracking()
            .Where(customer => customer.is_active != false && customer.name != null)
            .Select(customer => customer.name)
            .ToListAsync(cancellationToken);

        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            var key = _parser.NormalizeOrganizationName(name);
            if (!string.IsNullOrWhiteSpace(key))
            {
                normalized.Add(key);
            }
        }

        return normalized;
    }
}
