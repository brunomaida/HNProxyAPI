using System.Net;
using FluentAssertions;
using HNProxyAPI.Data;
using HNProxyAPI.Services;
using HNProxyAPI.Settings;
using HNProxyAPI.Tests.Integration.Setup;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Moq.Protected;

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
        public async Task GlobalTimeout_Should_Return_504_When_Upstream_Is_Slow_Async()
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

            // Since this is a new isolated server, the cache is empty, forcing a network call to our slow mock
            var response = await strictClient.GetAsync("/api/beststories?n=1");

            var erroReal = await response.Content.ReadAsStringAsync();
            // #ASSERT
            // The GlobalTimeout middleware should catch the cancellation and return 504 Gateway Timeout
            response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
        }

        [Fact]
        public async Task RateLimiter_Should_Block_Third_Request()
        {
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((ctx, conf) =>
                {
                    conf.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        {"InboundAPISettings:UrlBase", "http://localhost"},
                        {"InboundAPISettings:GlobalRequestTimeoutMs", "5000"},
                        {"InboundAPISettings:RateLimitWindowSeconds", "10"},
                        {"InboundAPISettings:MaxRequestsPerWindow", "2"},
                        {"InboundAPISettings:QueueLimit", "0"}
                    });
                });

                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IHackerNewsClient>();
                    services.RemoveAll<IStoryCache>();
                    services.RemoveAll<ILoggerProvider>(); // Silencia logs de erro
                    services.PostConfigure<InboundAPISettings>(options =>
                    {
                        options.MaxRequestsPerWindow = 2;
                        options.RateLimitWindowSeconds = 10;
                        options.QueueLimit = 0;
                    });

                    var mockClient = new Mock<IHackerNewsClient>();
                    mockClient.Setup(c => c.GetCurrentStoriesAsync(It.IsAny<CancellationToken>()))
                              .ReturnsAsync(new int[] { 1, 2 });

                    var mockCache = new Mock<IStoryCache>();
                    mockCache.Setup(c => c.GetOrderedList())
                             .Returns(new List<Story> { new Story { id = 1, title = "A", score = 10 } });

                    services.AddSingleton(mockCache.Object);
                    services.AddSingleton(mockClient.Object);

                    services.AddSingleton<IStartupFilter>(new RateLimitTestFilter());
                });

            }).CreateClient();

            var url = "/api/beststories?n=1";

            // #ASSERT
            var res1 = await client.GetAsync(url);
            res1.StatusCode.Should().Be(HttpStatusCode.OK);

            var res2 = await client.GetAsync(url);
            res2.StatusCode.Should().Be(HttpStatusCode.OK);

            var res3 = await client.GetAsync(url);
            res3.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        }

        // Classe auxiliar mínima para configurar o ambiente de teste
        public class RateLimitTestFilter : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            {
                return app =>
                {
                    app.Use(async (context, nextMiddleware) =>
                    {
                        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");
                        await nextMiddleware();
                    });

                    next(app);

                    app.Map("/teste-rate-limit", builder =>
                    {
                        builder.Run(async ctx => await ctx.Response.WriteAsync("Rate limit check."));
                    });
                };
            }
        }
    }
}
