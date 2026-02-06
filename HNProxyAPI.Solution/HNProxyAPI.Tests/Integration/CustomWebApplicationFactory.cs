using HNProxyAPI.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Moq;

namespace HNProxyAPI.Tests.Integration.Setup
{
    public class CustomWebApplicationFactory : WebApplicationFactory<Program>
    {
        // Holds the Mock Handler to configure responses per test
        public Mock<HttpMessageHandler> MockHttpHandler { get; }

        public CustomWebApplicationFactory()
        {
            MockHttpHandler = new Mock<HttpMessageHandler>();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Force environment to specific value if needed
            builder.UseEnvironment("Testing");

            builder.UseContentRoot(Directory.GetCurrentDirectory());

            // Override Configuration (appsettings)
            builder.ConfigureAppConfiguration((context, conf) =>
            {
                var newConfig = new Dictionary<string, string>
                {
                    // Use a fake URL to ensure we never hit the real API
                    {"HackerNewsAPISettings:UrlBase", "http://fake-hn-api.com/beststories.json"},
                    {"HackerNewsAPISettings:UrlBaseStoryById", "http://fake-hn-api.com/item/{0}.json"},
                    {"HackerNewsAPISettings:MaxMemoryThresholdBytes", "500000"}, // 1GB                    
                    {"HackerNewsAPISettings:RequestTimeoutMs", "10000"},
                    {"InboundAPISettings:MaxRequestsPerWindow", "100"}, // High default, override in specific tests
                    {"InboundAPISettings:RateLimitWindowSeconds", "10"},
                    {"InboundAPISettings:GlobalRequestTimeoutMs", "20000"}
                };

                conf.AddInMemoryCollection(newConfig);
            });

            // Configure Services (Dependency Injection)
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient<IHackerNewsClient, HackerNewsClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => MockHttpHandler.Object);

                // Disable excessive logging in tests
                services.AddLogging(logging => logging.ClearProviders());
            });
        }
    }
}
