using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;

namespace Telesale.Api.Services;

public class LocalGovernmentImportPreviewService : ILocalGovernmentImportPreviewService
{
    private readonly TelesaleDbContext _db;
    private readonly ILocalGovernmentImportParserService _parser;

    public LocalGovernmentImportPreviewService(
        TelesaleDbContext db,
        ILocalGovernmentImportParserService parser)
    {
        _db = db;
        _parser = parser;
    }

    public async Task<LocalGovernmentImportPreviewSummary> PreviewFileAsync(
        Stream stream,
        string extension,
        CancellationToken cancellationToken)
    {
        var existingNames = await LoadExistingNormalizedOrganizationNamesAsync(cancellationToken);
        return await _parser.ParsePreviewFileAsync(stream, extension, existingNames, cancellationToken);
    }

    public async Task<LocalGovernmentImportPreviewSummary> PreviewAsync(
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string?>> rows,
        CancellationToken cancellationToken)
    {
        var existingNames = await LoadExistingNormalizedOrganizationNamesAsync(cancellationToken);
        return _parser.ParsePreview(headers, rows, existingNames);
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
