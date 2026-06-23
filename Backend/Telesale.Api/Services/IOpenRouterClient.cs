namespace Telesale.Api.Services;

public interface IOpenRouterClient
{
    Task<string?> SummarizeAsync(OpenRouterPrompt prompt, CancellationToken cancellationToken);
}

public sealed record OpenRouterPrompt(string SystemPrompt, string UserPrompt);
