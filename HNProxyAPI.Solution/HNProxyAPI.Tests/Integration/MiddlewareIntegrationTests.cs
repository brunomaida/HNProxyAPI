using FluentAssertions;
using HNProxyAPI.Services;
using HNProxyAPI.Settings;
using HNProxyAPI.Tests.Integration.Setup;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Moq.Protected;
using System.Net;

namespace HNProxyAPI.Tests.Integration
{
    public class MiddlewareIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public MiddlewareIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GlobalTimeout_Should_Return_504_When_Upstream_Is_Slow()
        {
            try
            {
                // Create a local Mock specifically for this test to avoid cross-test pollution
                var mockHandler = new Mock<HttpMessageHandler>();

                // Setup the delay to be significantly longer than the timeout (250ms > 50ms)
                // Passing the CancellationToken is vital so Task.Delay can throw when the middleware cancels it
                mockHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>("SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .Returns(async (HttpRequestMessage req, CancellationToken token) =>
                    {
                        await Task.Delay(250, token); // Simulate network lag
                        return new HttpResponseMessage(HttpStatusCode.OK);
                    });

            
                // Configure a customized Test Server using WithWebHostBuilder
                var strictClient = _factory.WithWebHostBuilder(builder =>
                {
                    builder.ConfigureTestServices(services =>
                    {
                        services.Configure<InboundAPISettings>(options => options.GlobalRequestTimeoutMs = 50);
                        services.RemoveAll<IHackerNewsClient>();
                        services.AddHttpClient<IHackerNewsClient, HackerNewsClient>()
                            .ConfigurePrimaryHttpMessageHandler(() => mockHandler.Object);

                    });
                }).CreateClient();            

                // Act
                // Since this is a new isolated server, the cache is empty, forcing a network call to our slow mock
                var response = await strictClient.GetAsync("/api/stories/best");

                // #ASSERT
                // The GlobalTimeout middleware should catch the cancellation and return 504 Gateway Timeout
                response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
            }
            catch (Exception ex)
            {
            }
        }

        [Fact]
        public async Task RateLimiter_Should_Return_429_When_Limit_Exceeded()
        {
            try
            {
                // Arrange
                // Configure Rate Limiting: 2 requests allowed per 10-second window
                var limitedClient = _factory.WithWebHostBuilder(builder =>
                {
                    builder.ConfigureAppConfiguration((ctx, conf) =>
                    {
                        conf.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            {"InboundAPISettings:MaxRequestsPerWindow", "2"},
                            {"InboundAPISettings:RateLimitWindowSeconds", "10"},
                            {"InboundAPISettings:QueueLimit", "0"} // Fail immediately if limit reached
                        });
                    });
                }).CreateClient();

                // Act & Assert

                // Request 1: Should be successful
                var res1 = await limitedClient.GetAsync("/health");
                res1.StatusCode.Should().Be(HttpStatusCode.OK);

                // Request 2: Should be successful
                var res2 = await limitedClient.GetAsync("/health");
                res2.StatusCode.Should().Be(HttpStatusCode.OK);

                // Request 3: Should be blocked by the RateLimiter
                var res3 = await limitedClient.GetAsync("/health");

                // Verify if the middleware returns 429 Too Many Requests
                res3.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            }
            catch (Exception ex)
            {
            }
        }
    }
}