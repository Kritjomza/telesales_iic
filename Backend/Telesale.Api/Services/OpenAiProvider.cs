using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Telesale.Api.Services;

public class OpenAiProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAiProvider> _logger;
    private readonly string? _apiKey;

    public string Name => "OpenAI";

    public OpenAiProvider(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAiProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Ai:OpenAI:ApiKey"] ?? configuration["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    }

    public async Task<string?> GenerateContentAsync(string prompt, bool requireJson = false)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("OpenAI API key is not configured.");
            return null;
        }

        try
        {
            var url = "https://api.openai.com/v1/chat/completions";
            
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            object requestPayload;
            if (requireJson)
            {
                requestPayload = new
                {
                    model = "gpt-4o-mini",
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    response_format = new { type = "json_object" }
                };
            }
            else
            {
                requestPayload = new
                {
                    model = "gpt-4o-mini",
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    }
                };
            }

            var jsonPayload = JsonSerializer.Serialize(requestPayload);
            requestMessage.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode)
            {
                var errText = await response.Content.ReadAsStringAsync();
                _logger.LogError($"OpenAI API error: status {response.StatusCode}, details: {errText}");
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using (var doc = JsonDocument.Parse(responseJson))
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("choices", out var choices) && 
                    choices.GetArrayLength() > 0 &&
                    choices[0].TryGetProperty("message", out var messageObj) &&
                    messageObj.TryGetProperty("content", out var contentProp))
                {
                    return contentProp.GetString();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed calling OpenAI API.");
        }

        return null;
    }
}
