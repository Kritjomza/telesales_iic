namespace Telesale.Api.Services;

public interface IImportNormalizationService
{
    string? CleanText(string? value);
    string NormalizePhoneNumber(string phone);
    string NormalizeCapital(string capital);
}
