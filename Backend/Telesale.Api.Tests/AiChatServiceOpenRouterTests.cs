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

        var response = await service.SendMessageAsync("company Apex", null, null, null, new ClaimsPrincipal(), default);

        Assert.Equal("Apex Medical is a New healthcare customer in Bangkok.", response.Reply);
        Assert.Equal("ai_tool_answer", response.Metadata.Source);
        Assert.True(response.Metadata.UsedAi);
        Assert.Equal(1, response.Metadata.MatchedCustomersCount);
        Assert.Equal(1, openRouter.SummaryCallCount);
    }

    [Fact]
    public async Task SendMessage_ReturnsAiToolAnswer_WhenNoCustomerContextMatches()
    {
        var contextService = new StubCustomerContextService(new CustomerContextResult(Array.Empty<CustomerContextCustomer>()));
        var openRouter = new StubOpenRouterClient("should not be used");
        var service = new AiChatService(contextService, openRouter, new AiChatPromptBuilder());

        var response = await service.SendMessageAsync("company Unknown", null, null, null, new ClaimsPrincipal(), default);

        Assert.Equal("should not be used", response.Reply);
        Assert.Equal("ai_tool_answer", response.Metadata.Source);
        Assert.True(response.Metadata.UsedAi);
        Assert.Equal(0, response.Metadata.MatchedCustomersCount);
        Assert.Equal(1, openRouter.SummaryCallCount);
    }

    [Fact]
    public async Task SendMessage_UsesOpenRouterFinalAnswer_WhenMultipleCustomersMatch()
    {
        var contextService = new StubCustomerContextService(new CustomerContextResult(new[]
        {
            Customer(1, "Apex Medical"),
            Customer(2, "Apex Logistics")
        }));
        var openRouter = new StubOpenRouterClient("should not be used");
        var service = new AiChatService(contextService, openRouter, new AiChatPromptBuilder());

        var response = await service.SendMessageAsync("company Apex", null, null, null, new ClaimsPrincipal(), default);

        Assert.Equal("should not be used", response.Reply);
        Assert.Equal("ai_tool_answer", response.Metadata.Source);
        Assert.True(response.Metadata.UsedAi);
        Assert.Equal(2, response.Metadata.MatchedCustomersCount);
        Assert.Equal(1, openRouter.SummaryCallCount);
    }

    [Fact]
    public async Task SendMessage_ReturnsDatabaseFallback_WhenOpenRouterFails()
    {
        var contextService = new StubCustomerContextService(SingleCustomerContext());
        var openRouter = new StubOpenRouterClient(null);
        var service = new AiChatService(contextService, openRouter, new AiChatPromptBuilder());

        var response = await service.SendMessageAsync("company Apex", null, null, null, new ClaimsPrincipal(), default);

        Assert.Contains("Apex Medical", response.Reply);
        Assert.Contains("02-111-2222", response.Reply);
        Assert.Equal("database_fallback", response.Metadata.Source);
        Assert.False(response.Metadata.UsedAi);
        Assert.Equal(1, response.Metadata.MatchedCustomersCount);
    }

    [Fact]
    public async Task SendMessage_UsesOpenRouterIntentJson_ToRouteGlobalNearExpiry()
    {
        var contextService = new CapturingCustomerContextService(new CustomerContextResult(new[]
        {
            Customer(1, "Apex Medical")
        }, IsGlobalNearExpiry: true));
        var openRouter = new StubOpenRouterClient(
            "ลูกค้าใกล้หมดอายุคือ Apex Medical",
            """{"intent":"global_near_expiry","companyKeyword":null,"isFollowUp":false,"needsGlobalSearch":true,"limit":5,"sortBy":"renewalDays"}""");
        var service = new AiChatService(contextService, openRouter, new AiChatPromptBuilder());

        var response = await service.SendMessageAsync("ขอบริษัทที่ใกล้หมดอายุ 5 ลำดับแรก", null, 10, "Old Customer", new ClaimsPrincipal(), default);

        Assert.Equal("ลูกค้าใกล้หมดอายุคือ Apex Medical", response.Reply);
        Assert.Equal("ai_tool_answer", response.Metadata.Source);
        Assert.NotNull(contextService.LastRequest);
        Assert.Equal(AiChatToolAction.GetNearExpiryCustomers, contextService.LastRequest!.ToolAction);
        Assert.True(contextService.LastRequest.NeedsGlobalSearch);
        Assert.Equal(5, contextService.LastRequest.Limit);
        Assert.Null(contextService.LastRequest.ContextCustomerId);
    }

    [Fact]
    public void BuildSummaryPrompt_ContainsDatabaseContextRules_AndOmitsHiddenConfig()
    {
        var prompt = new AiChatPromptBuilder().BuildSummaryPrompt("company Apex", SingleCustomerContext());

        Assert.Contains("Answer only from the provided database context.", prompt.SystemPrompt);
        Assert.Contains("ไม่พบข้อมูลในระบบ", prompt.SystemPrompt);
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

    [Fact]
    public async Task OpenRouterClient_ReturnsNullAfterRetry_WhenProviderRateLimits()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var client = CreateOpenRouterClient(handler);

        var result = await client.SummarizeAsync(
            new OpenRouterPrompt("system rules", "database context"),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task OpenRouterClient_ReturnsNull_WhenResponseContentIsInvalid()
    {
        var handler = new SequenceHandler(JsonResponse("""{"choices":[]}"""));
        var client = CreateOpenRouterClient(handler);

        var result = await client.SummarizeAsync(
            new OpenRouterPrompt("system rules", "database context"),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.RequestCount);
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
            null,
            null,
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

    private static OpenRouterClient CreateOpenRouterClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new OpenRouterOptions
        {
            ApiKey = "test-key",
            BaseUrl = "https://openrouter.ai/api/v1/chat/completions",
            Model = "openrouter/free-model",
            TimeoutSeconds = 5,
            MaxTokens = 200
        });

        return new OpenRouterClient(httpClient, options, NullLogger<OpenRouterClient>.Instance);
    }

    private sealed class StubCustomerContextService : ICustomerContextService
    {
        private readonly CustomerContextResult _result;

        public StubCustomerContextService(CustomerContextResult result)
        {
            _result = result;
        }

        public Task<CustomerContextResult> GetCustomerContextAsync(
            CustomerContextRequest request,
            ClaimsPrincipal user,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class StubOpenRouterClient : IOpenRouterClient
    {
        private readonly string? _summary;
        private readonly string? _intentJson;

        public StubOpenRouterClient(string? summary, string? intentJson = null)
        {
            _summary = summary;
            _intentJson = intentJson;
        }

        public int SummaryCallCount { get; private set; }

        public Task<string?> InterpretIntentAsync(OpenRouterPrompt prompt, CancellationToken cancellationToken)
        {
            return Task.FromResult(_intentJson);
        }

        public Task<string?> SummarizeAsync(OpenRouterPrompt prompt, CancellationToken cancellationToken)
        {
            SummaryCallCount++;
            return Task.FromResult(_summary);
        }
    }

    private sealed class CapturingCustomerContextService : ICustomerContextService
    {
        private readonly CustomerContextResult _result;

        public CapturingCustomerContextService(CustomerContextResult result)
        {
            _result = result;
        }

        public CustomerContextRequest? LastRequest { get; private set; }

        public Task<CustomerContextResult> GetCustomerContextAsync(
            CustomerContextRequest request,
            ClaimsPrincipal user,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_result);
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
