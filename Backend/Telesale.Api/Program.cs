using System.Net;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;
using Telesale.Api.Helpers;
using Telesale.Api.Services;

var builder = WebApplication.CreateBuilder(args);
EnvLoader.Load(Path.Combine(builder.Environment.ContentRootPath, ".env"));

const string ReactDevCorsPolicy = "ReactDevCorsPolicy";

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 2;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ATS_Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            },
            OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy(ReactDevCorsPolicy, policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for cookies to be sent across different ports during development
    });
});

var connectionString = builder.Configuration.GetConnectionString("TelesaleDb")
    ?? throw new InvalidOperationException("Missing connection string: TelesaleDb");

builder.Services.AddDbContext<TelesaleDbContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

builder.Services.AddHttpClient();
builder.Services.AddHttpClient<Telesale.Api.Services.GeminiProvider>();
builder.Services.AddHttpClient<Telesale.Api.Services.ClaudeProvider>();
builder.Services.AddHttpClient<Telesale.Api.Services.OpenAiProvider>();
builder.Services.AddHttpClient<OpenRouterClient>();

builder.Services.Configure<OpenRouterOptions>(options =>
{
    options.ApiKey = builder.Configuration["Ai:OpenRouter:ApiKey"]
        ?? builder.Configuration["OpenRouter:ApiKey"]
        ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
    options.BaseUrl = builder.Configuration["Ai:OpenRouter:BaseUrl"]
        ?? Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL")
        ?? options.BaseUrl;
    options.Model = builder.Configuration["Ai:OpenRouter:Model"]
        ?? Environment.GetEnvironmentVariable("OPENROUTER_MODEL")
        ?? options.Model;

    if (int.TryParse(builder.Configuration["Ai:OpenRouter:TimeoutSeconds"]
            ?? Environment.GetEnvironmentVariable("OPENROUTER_TIMEOUT_SECONDS"), out var timeoutSeconds))
    {
        options.TimeoutSeconds = timeoutSeconds;
    }

    if (int.TryParse(builder.Configuration["Ai:OpenRouter:MaxTokens"]
            ?? Environment.GetEnvironmentVariable("OPENROUTER_MAX_TOKENS"), out var maxTokens))
    {
        options.MaxTokens = maxTokens;
    }
});

builder.Services.AddScoped<Telesale.Api.Services.AiProviderFactory>();
builder.Services.AddScoped<Telesale.Api.Services.ICustomerContextService, Telesale.Api.Services.CustomerContextService>();
builder.Services.AddSingleton<AiChatPromptBuilder>();
builder.Services.AddScoped<IOpenRouterClient>(provider => provider.GetRequiredService<OpenRouterClient>());
builder.Services.AddScoped<Telesale.Api.Services.IAiChatService, Telesale.Api.Services.AiChatService>();
builder.Services.AddScoped<Telesale.Api.Services.IEmailNotificationService, Telesale.Api.Services.EmailNotificationService>();
builder.Services.AddScoped<Telesale.Api.Services.IImportNormalizationService, Telesale.Api.Services.ImportNormalizationService>();
builder.Services.AddScoped<Telesale.Api.Services.IAiExtractionService, Telesale.Api.Services.AiExtractionService>();
builder.Services.AddScoped<Telesale.Api.Services.IImportAiExtractionService, Telesale.Api.Services.ImportAiExtractionService>();
builder.Services.AddScoped<Telesale.Api.Services.IImportValidationService, Telesale.Api.Services.ImportValidationService>();
builder.Services.AddScoped<Telesale.Api.Services.IImportDuplicateDetectionService, Telesale.Api.Services.ImportDuplicateDetectionService>();
builder.Services.AddScoped<Telesale.Api.Services.IImportColumnMappingService, Telesale.Api.Services.ImportColumnMappingService>();
builder.Services.AddScoped<Telesale.Api.Services.IImportPolicyService, Telesale.Api.Services.ImportPolicyService>();
builder.Services.AddScoped<Telesale.Api.Services.ILocalGovernmentImportParserService, Telesale.Api.Services.LocalGovernmentImportParserService>();
builder.Services.AddScoped<Telesale.Api.Services.ILocalGovernmentImportPreviewService, Telesale.Api.Services.LocalGovernmentImportPreviewService>();
builder.Services.AddScoped<Telesale.Api.Services.ILocalGovernmentImportConfirmService, Telesale.Api.Services.LocalGovernmentImportConfirmService>();

var app = builder.Build();

var initializerSetting = Environment.GetEnvironmentVariable("RUN_DATABASE_INITIALIZER");
var runInitializer = DatabaseInitializerPolicy.ShouldRun(
    initializerSetting,
    app.Environment.IsDevelopment(),
    out var invalidInitializerSetting);

if (invalidInitializerSetting)
{
    app.Logger.LogWarning(
        "RUN_DATABASE_INITIALIZER has an invalid value. Database initialization is disabled.");
}

if (runInitializer)
{
    app.Logger.LogWarning("Database initialization is enabled for startup.");
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<TelesaleDbContext>();
            await DatabaseInitializer.InitializeAsync(context);
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while initializing the database.");
        }
    }
}
else
{
    app.Logger.LogInformation(
        "Database initialization skipped. Set RUN_DATABASE_INITIALIZER=true to enable it.");
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();

// เปิด CORS ก่อน MapControllers
app.UseCors(ReactDevCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
