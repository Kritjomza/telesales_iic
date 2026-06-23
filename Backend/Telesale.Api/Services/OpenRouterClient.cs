using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Telesale.Api.Services;

public sealed class OpenRouterClient : IOpenRouterClient
{
    private readonly HttpClient _httpClient;
    private readonly OpenRouterOptions _options;
    private readonly ILogger<OpenRouterClient> _logger;

    public OpenRouterClient(
        HttpClient httpClient,
        IOptions<OpenRouterOptions> options,
        ILogger<OpenRouterClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> SummarizeAsync(OpenRouterPrompt prompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("OpenRouter API key is not configured.");
            return null;
        }

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));

                using var request = BuildRequest(prompt);
                using var response = await _httpClient.SendAsync(request, timeoutCts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("OpenRouter summary request failed with status {StatusCode}.", (int)response.StatusCode);
                    if (attempt == 1)
                    {
                        continue;
                    }

                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync(timeoutCts.Token);
                return ExtractSummary(responseJson);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("OpenRouter summary request timed out.");
                if (attempt == 1)
                {
                    continue;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenRouter summary request failed.");
                if (attempt == 1)
                {
                    continue;
                }

                return null;
            }
        }

        return null;
    }

    private HttpRequestMessage BuildRequest(OpenRouterPrompt prompt)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var payload = new
        {
            model = _options.Model,
            max_tokens = _options.MaxTokens,
            temperature = 0.2,
            messages = new[]
            {
                new { role = "system", content = prompt.SystemPrompt },
                new { role = "user", content = prompt.UserPrompt }
            }
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        return request;
    }

    private static string? ExtractSummary(string responseJson)
    {
        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;

        if (!root.TryGetProperty("choices", out var choices) ||
            choices.GetArrayLength() == 0 ||
            !choices[0].TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var content))
        {
            return null;
        }

        var summary = content.GetString();
        return string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
    }
}
