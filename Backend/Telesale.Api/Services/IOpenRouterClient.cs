namespace Telesale.Api.Services;

public interface IOpenRouterClient
{
    Task<string?> InterpretIntentAsync(OpenRouterPrompt prompt, CancellationToken cancellationToken);

    Task<string?> SummarizeAsync(OpenRouterPrompt prompt, CancellationToken cancellationToken);
}

public sealed record OpenRouterPrompt(string SystemPrompt, string UserPrompt);
