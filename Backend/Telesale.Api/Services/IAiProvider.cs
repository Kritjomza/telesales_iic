using System.Threading.Tasks;

namespace Telesale.Api.Services;

public interface IAiProvider
{
    string Name { get; }
    Task<string?> GenerateContentAsync(string prompt, bool requireJson = false);
}
