namespace Telesale.Api.Services;

public interface ILocalGovernmentImportParserService
{
    Task<LocalGovernmentImportPreviewSummary> ParsePreviewFileAsync(
        Stream stream,
        string extension,
        ISet<string> existingNormalizedOrganizationNames,
        CancellationToken cancellationToken);

    LocalGovernmentImportPreviewSummary ParsePreview(
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string?>> rows,
        ISet<string> existingNormalizedOrganizationNames);

    string? NormalizeOrganizationName(string? value);
}
