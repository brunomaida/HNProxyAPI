using HNProxyAPI.Extensions;
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
// Tried to use latest Swachbuckle versions without success
/*builder.Services.AddSwaggerGen(sgo =>
{
    sgo.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo()
    { 
        Title = "HNProxyAPI",
        Version = "v1",
        Description = "HackerNews Broker - Query first N best stories",        
    });
    sgo.CustomSchemaIds(type => type.ToString());
});*/

builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Logging.AddAzureWebAppDiagnostics();
builder.Services.Configure<AzureFileLoggerOptions>(
    builder.Configuration.GetSection("Logging:AzureAppServicesFile"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    /*
    app.UseSwagger(c =>
    {
        c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
        {
            //  Forces specific version in the header 3.0.1 to achieve UI compatibility
            swaggerDoc.Info.Version = "3.0.1";
        });
    });
    app.UseSwaggerUI(o => {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "HNProxyAPI v1");
        o.RoutePrefix = string.Empty;
        o.EnableValidator(null);
    });*/
}

app.Services.GetRequiredService<SystemMetricsCollector>();
app.UseHttpsRedirection();

// Order is important: RateLimit -> Timeout -> Auth -> Controllers
app.UseRateLimiter();
app.UseMiddleware<GlobalTimeout>();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers().RequireRateLimiting("GlobalPolicy");

app.Run();

public partial class Program { } // This is needed for testing purposes: class needs to be visible 
