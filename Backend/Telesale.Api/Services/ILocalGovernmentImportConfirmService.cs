namespace Telesale.Api.Services;

public interface ILocalGovernmentImportConfirmService
{
    Task<LocalGovernmentImportConfirmResult> ConfirmFileAsync(
        Stream stream,
        string extension,
        uint userId,
        CancellationToken cancellationToken);

    Task<LocalGovernmentImportConfirmResult> ConfirmAsync(
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string?>> rows,
        uint userId,
        CancellationToken cancellationToken);
}
