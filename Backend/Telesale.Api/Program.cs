using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Telesale.Api.Data;
using Telesale.Api.Helpers;

var builder = WebApplication.CreateBuilder(args);
EnvLoader.Load(Path.Combine(builder.Environment.ContentRootPath, ".env"));

const string ReactDevCorsPolicy = "ReactDevCorsPolicy";

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

builder.Services.AddScoped<Telesale.Api.Services.AiProviderFactory>();
builder.Services.AddScoped<Telesale.Api.Services.IEmailNotificationService, Telesale.Api.Services.EmailNotificationService>();
builder.Services.AddScoped<Telesale.Api.Services.IImportNormalizationService, Telesale.Api.Services.ImportNormalizationService>();
builder.Services.AddScoped<Telesale.Api.Services.IAiExtractionService, Telesale.Api.Services.AiExtractionService>();
builder.Services.AddScoped<Telesale.Api.Services.IImportAiExtractionService, Telesale.Api.Services.ImportAiExtractionService>();
builder.Services.AddScoped<Telesale.Api.Services.IImportValidationService, Telesale.Api.Services.ImportValidationService>();
builder.Services.AddScoped<Telesale.Api.Services.IImportDuplicateDetectionService, Telesale.Api.Services.ImportDuplicateDetectionService>();
builder.Services.AddScoped<Telesale.Api.Services.IImportColumnMappingService, Telesale.Api.Services.ImportColumnMappingService>();
builder.Services.AddScoped<Telesale.Api.Services.IImportPolicyService, Telesale.Api.Services.ImportPolicyService>();

var app = builder.Build();

// Initialize the database schema and seed data
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// เปิด CORS ก่อน MapControllers
app.UseCors(ReactDevCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();