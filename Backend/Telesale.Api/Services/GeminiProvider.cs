using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Telesale.Api.Services;

public class GeminiProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiProvider> _logger;
    private readonly string? _apiKey;

    public string Name => "Gemini";

    public GeminiProvider(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Ai:Gemini:ApiKey"] ?? configuration["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
    }

    public async Task<string?> GenerateContentAsync(string prompt, bool requireJson = false)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("Gemini API key is not configured.");
            return null;
        }

        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";
            
            var requestPayload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = requireJson ? "application/json" : "text/plain"
                }
            };

            var jsonPayload = JsonSerializer.Serialize(requestPayload);
            using (var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json"))
            {
                var response = await _httpClient.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    var errText = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Gemini API error: status {response.StatusCode}, details: {errText}");
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                using (var doc = JsonDocument.Parse(responseJson))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("candidates", out var candidates) && 
                        candidates.GetArrayLength() > 0 &&
                        candidates[0].TryGetProperty("content", out var contentObj) &&
                        contentObj.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0 &&
                        parts[0].TryGetProperty("text", out var textProp))
                    {
                        return textProp.GetString();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed calling Gemini API.");
        }

        return null;
    }
}
