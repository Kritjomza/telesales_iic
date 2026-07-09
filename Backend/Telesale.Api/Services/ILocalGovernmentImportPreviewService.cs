namespace Telesale.Api.Services;

public interface ILocalGovernmentImportPreviewService
{
    Task<LocalGovernmentImportPreviewSummary> PreviewFileAsync(
        Stream stream,
        string extension,
        CancellationToken cancellationToken);

    Task<LocalGovernmentImportPreviewSummary> PreviewAsync(
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string?>> rows,
        CancellationToken cancellationToken);
}
