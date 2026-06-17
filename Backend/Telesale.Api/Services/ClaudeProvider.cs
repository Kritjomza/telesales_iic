using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Telesale.Api.Services;

public class ClaudeProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClaudeProvider> _logger;
    private readonly string? _apiKey;

    public string Name => "Claude";

    public ClaudeProvider(HttpClient httpClient, IConfiguration configuration, ILogger<ClaudeProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Ai:Claude:ApiKey"] ?? configuration["Claude:ApiKey"] ?? Environment.GetEnvironmentVariable("CLAUDE_API_KEY");
    }

    public async Task<string?> GenerateContentAsync(string prompt, bool requireJson = false)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("Claude API key is not configured.");
            return null;
        }

        try
        {
            var url = "https://api.anthropic.com/v1/messages";
            
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Headers.Add("x-api-key", _apiKey);
            requestMessage.Headers.Add("anthropic-version", "2023-06-01");

            // Anthropic prompts for JSON are typically handled by instructions inside the prompt itself,
            // as Anthropic does not have a formal enforce-json flag like OpenAI, but they recommend 
            // putting JSON instructions at the end of prompt or system prompt.
            var requestPayload = new
            {
                model = "claude-3-5-haiku-20241022",
                max_tokens = 2048,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var jsonPayload = JsonSerializer.Serialize(requestPayload);
            requestMessage.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode)
            {
                var errText = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Claude API error: status {response.StatusCode}, details: {errText}");
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using (var doc = JsonDocument.Parse(responseJson))
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("content", out var contentArray) && 
                    contentArray.GetArrayLength() > 0 &&
                    contentArray[0].TryGetProperty("text", out var textProp))
                {
                    return textProp.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed calling Claude API.");
        }

        return null;
    }
}
