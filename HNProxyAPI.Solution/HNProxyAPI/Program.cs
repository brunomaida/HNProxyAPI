using HNProxyAPI.Extensions;
using HNProxyAPI.Log;
using HNProxyAPI.Metrics;
using HNProxyAPI.Middlewares;
using Microsoft.Extensions.Logging.AzureAppServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppConfiguration();
builder.Services.AddHackerNewsInfrastructure(); // System Core: HttpClient, Services, Metrics
builder.Services.AddCustomRateLimiting();       // System Security and Performance
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddChannelFileLogger("logs/hnproxyapi_log.txt");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Order is important: Timeout -> RateLimit -> Auth -> Controllers
app.UseMiddleware<GlobalTimeout>();
app.Services.GetRequiredService<SystemMetricsCollector>();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers().RequireRateLimiting("GlobalPolicy");

app.Run();

public partial class Program { } // This is needed for testing purposes: class needs to be visible 
