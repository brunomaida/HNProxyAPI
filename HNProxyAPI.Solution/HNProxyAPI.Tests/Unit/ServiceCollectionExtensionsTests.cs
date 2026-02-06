using FluentAssertions;
using HNProxyAPI.Data;
using HNProxyAPI.Extensions;
using HNProxyAPI.Services;
using HNProxyAPI.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace HNProxyAPI.Tests.Unit
{
    public class ServiceCollectionExtensionsTests
    {
        private readonly IConfiguration _configuration;

        public ServiceCollectionExtensionsTests()
        {
            // Fake configuration for appsettings.json simulation
            var myConfiguration = new Dictionary<string, string>
            {
                {"HackerNewsAPISettings:UrlBase", "https://test-api.com/"},
                {"HackerNewsAPISettings:MaxConcurrentRequests", "10"},
                {"HackerNewsAPISettings:RequestTimeoutMs", "5000"},
                {"HackerNewsAPISettings:MaxMemoryThresholdBytes", "100000"},
                {"InboundAPISettings:MaxRequestsPerWindow", "50"},
                {"InboundAPISettings:RateLimitWindowSeconds", "10"},
                {"InboundAPISettings:QueueLimit", "5"}
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(myConfiguration)
                .Build();
        }

        [Fact]
        public void AddAppConfiguration_Should_Bind_And_Validate_Settings()
        {
            var services = new ServiceCollection();
            services.AddSingleton(_configuration); 
            services.AddAppConfiguration();

            var provider = services.BuildServiceProvider();
            var hnSettings = provider.GetService<IOptions<HackerNewsServiceSettings>>();
            var inboundSettings = provider.GetService<IOptions<InboundAPISettings>>();

            // #ASSERT
            hnSettings?.Value.UrlBase.Should().Be("https://test-api.com/");
            inboundSettings?.Value.MaxRequestsPerWindow.Should().Be(50);
        }

        [Fact]
        public void AddHackerNewsInfrastructure_Should_Register_Core_Services()
        {
            var services = new ServiceCollection();
            services.AddSingleton(_configuration);
            services.AddAppConfiguration(); // Necessário pois a infra depende das settings
            services.AddLogging(); // Necessário para os loggers internos
            services.AddHackerNewsInfrastructure();

            var cacheDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IStoryCache));
            cacheDescriptor.Should().NotBeNull();
            cacheDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);

            var queryDescriptor = services.FirstOrDefault(s => s.ServiceType == typeof(HackerNewsQueryService));
            queryDescriptor.Should().NotBeNull();
            queryDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);

            var hostedService = services.FirstOrDefault(s => s.ImplementationType == typeof(HackerNewsCacheWarmUpService));

            // #ASSERT
            hostedService.Should().NotBeNull();
        }

        [Fact]
        public void AddHackerNewsInfrastructure_Should_Configure_HttpClient_Correctly()
        {
            var services = new ServiceCollection();
            services.AddSingleton(_configuration);
            services.AddAppConfiguration();
            services.AddLogging();
            services.AddHackerNewsInfrastructure();

            var provider = services.BuildServiceProvider();
            var clientFactory = provider.GetRequiredService<IHttpClientFactory>();
            var client = clientFactory.CreateClient(typeof(IHackerNewsClient).Name);

            // #ASSERT
            client.BaseAddress.Should().Be(new Uri("https://test-api.com/"));
            client.Timeout.Should().Be(Timeout.InfiniteTimeSpan);
        }

        [Fact]
        public void AddCustomRateLimiting_Should_Register_RateLimiter_Services()
        {
            var services = new ServiceCollection();
            services.AddSingleton(_configuration);
            services.AddAppConfiguration();
            services.AddCustomRateLimiting();

            // #ASSERT
            services.Any(x => x.ServiceType.Name.Contains("RateLimiting")).Should().BeTrue();
        }
    }
}
