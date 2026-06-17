using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Telesale.Api.Services;

public class AiProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiProviderFactory> _logger;

    public AiProviderFactory(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<AiProviderFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public IAiProvider GetActiveProvider()
    {
        var providerSetting = _configuration["Ai:Provider"] ?? Environment.GetEnvironmentVariable("AI_PROVIDER") ?? "Gemini";
        
        switch (providerSetting.Trim().ToLowerInvariant())
        {
            case "openai":
                return _serviceProvider.GetRequiredService<OpenAiProvider>();
            case "claude":
                return _serviceProvider.GetRequiredService<ClaudeProvider>();
            case "gemini":
            default:
                return _serviceProvider.GetRequiredService<GeminiProvider>();
        }
    }
}
