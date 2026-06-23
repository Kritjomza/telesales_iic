namespace Telesale.Api.Services;

public sealed class OpenRouterOptions
{
    public string? ApiKey { get; set; }

    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1/chat/completions";

    public string Model { get; set; } = "nvidia/nemotron-3-ultra-550b-a55b:free";

    public int TimeoutSeconds { get; set; } = 15;

    public int MaxTokens { get; set; } = 300;
}
