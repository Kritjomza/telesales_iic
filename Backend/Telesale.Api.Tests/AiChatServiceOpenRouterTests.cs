using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telesale.Api.Models;
using Telesale.Api.Services;

namespace Telesale.Api.Tests;

public class AiChatServiceOpenRouterTests
{
    [Fact]
    public async Task SendMessage_ReturnsAiSummary_WhenCustomerContextMatches()
    {
        var contextService = new StubCustomerContextService(SingleCustomerContext());
        var openRouter = new StubOpenRouterClient("Apex Medical is a New healthcare customer in Bangkok.");
        var service = new AiChatService(contextService, openRouter, new AiChatPromptBuilder());

        var response = await service.SendMessageAsync("company Apex", null, new ClaimsPrincipal(), default);

        Assert.Equal("Apex Medical is a New healthcare customer in Bangkok.", response.Reply);
        Assert.Equal("ai_summary", response.Metadata.Source);
        Assert.True(response.Metadata.UsedAi);
        Assert.Equal(1, response.Metadata.MatchedCustomersCount);
        Assert.Equal(1, openRouter.CallCount);
    }

    [Fact]
    public async Task SendMessage_DoesNotCallOpenRouter_WhenNoCustomerContextMatches()
    {
        var contextService = new StubCustomerContextService(new CustomerContextResult(Array.Empty<CustomerContextCustomer>()));
        var openRouter = new StubOpenRouterClient("should not be used");
        var service = new AiChatService(contextService, openRouter, new AiChatPromptBuilder());

        var response = await service.SendMessageAsync("company Unknown", null, new ClaimsPrincipal(), default);

        Assert.Equal("No matching customer was found in the database.", response.Reply);
        Assert.Equal("database", response.Metadata.Source);
        Assert.False(response.Metadata.UsedAi);
        Assert.Equal(0, response.Metadata.MatchedCustomersCount);
        Assert.Equal(0, openRouter.CallCount);
    }

    [Fact]
    public async Task SendMessage_DoesNotCallOpenRouter_WhenMultipleCustomersMatch()
    {
        var contextService = new StubCustomerContextService(new CustomerContextResult(new[]
        {
            Customer(1, "Apex Medical"),
            Customer(2, "Apex Logistics")
        }));
        var openRouter = new StubOpenRouterClient("should not be used");
        var service = new AiChatService(contextService, openRouter, new AiChatPromptBuilder());

        var response = await service.SendMessageAsync("company Apex", null, new ClaimsPrincipal(), default);

        Assert.Contains("Apex Medical", response.Reply);
        Assert.Contains("Apex Logistics", response.Reply);
        Assert.Contains("specify", response.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("database", response.Metadata.Source);
        Assert.False(response.Metadata.UsedAi);
        Assert.Equal(2, response.Metadata.MatchedCustomersCount);
        Assert.Equal(0, openRouter.CallCount);
    }

    [Fact]
    public async Task SendMessage_ReturnsDatabaseFallback_WhenOpenRouterFails()
    {
        var contextService = new StubCustomerContextService(SingleCustomerContext());
        var openRouter = new StubOpenRouterClient(null);
        var service = new AiChatService(contextService, openRouter, new AiChatPromptBuilder());

        var response = await service.SendMessageAsync("company Apex", null, new ClaimsPrincipal(), default);

        Assert.Contains("Apex Medical", response.Reply);
        Assert.Contains("02-111-2222", response.Reply);
        Assert.Equal("database_fallback", response.Metadata.Source);
        Assert.False(response.Metadata.UsedAi);
        Assert.Equal(1, response.Metadata.MatchedCustomersCount);
    }

    [Fact]
    public void BuildSummaryPrompt_ContainsDatabaseContextRules_AndOmitsHiddenConfig()
    {
        var prompt = new AiChatPromptBuilder().BuildSummaryPrompt("company Apex", SingleCustomerContext());

        Assert.Contains("Answer only from the provided database context.", prompt.SystemPrompt);
        Assert.Contains("Apex Medical", prompt.UserPrompt);
        Assert.Contains("02-111-2222", prompt.UserPrompt);
        Assert.DoesNotContain("OPENROUTER_API_KEY", prompt.SystemPrompt + prompt.UserPrompt);
        Assert.DoesNotContain("test-key", prompt.SystemPrompt + prompt.UserPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpenRouterClient_RetriesOnce_WhenFirstRequestFails()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.InternalServerError),
            JsonResponse("""{"choices":[{"message":{"content":"AI summary"}}]}"""));
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenRouterOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://openrouter.ai/api/v1/chat/completions",
            Model = "openrouter/free-model",
            TimeoutSeconds = 5,
            MaxTokens = 200
        });
        var client = new OpenRouterClient(httpClient, options, NullLogger<OpenRouterClient>.Instance);

        var result = await client.SummarizeAsync(
            new OpenRouterPrompt("system rules", "database context"),
            CancellationToken.None);

        Assert.Equal("AI summary", result);
        Assert.Equal(2, handler.RequestCount);
    }

    private static CustomerContextResult SingleCustomerContext()
    {
        return new CustomerContextResult(new[]
        {
            Customer(1, "Apex Medical")
        });
    }

    private static CustomerContextCustomer Customer(uint id, string companyName)
    {
        return new CustomerContextCustomer(
            id,
            companyName,
            "02-111-2222",
            "Bangkok",
            "New",
            "Healthcare",
            new DateTime(2026, 6, 1),
            new[] { new CustomerContextContact("Narin", "081-111-2222", "narin@example.com") },
            Array.Empty<string>());
    }

    private static HttpResponseMessage JsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubCustomerContextService : ICustomerContextService
    {
        private readonly CustomerContextResult _result;

        public StubCustomerContextService(CustomerContextResult result)
        {
            _result = result;
        }

        public Task<CustomerContextResult> GetCustomerContextAsync(
            string message,
            uint? contextCustomerId,
            ClaimsPrincipal user,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class StubOpenRouterClient : IOpenRouterClient
    {
        private readonly string? _summary;

        public StubOpenRouterClient(string? summary)
        {
            _summary = summary;
        }

        public int CallCount { get; private set; }

        public Task<string?> SummarizeAsync(OpenRouterPrompt prompt, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_summary);
        }
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public SequenceHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
