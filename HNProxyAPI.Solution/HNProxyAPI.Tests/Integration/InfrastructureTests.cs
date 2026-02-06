using FluentAssertions;
using HNProxyAPI.Tests.Integration.Setup;
using Moq;
using Moq.Protected;
using System.Net;

namespace HNProxyAPI.Tests.Integration
{
    public class InfrastructureTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public InfrastructureTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task HealthCheck_Should_Return_Healthy_After_Warmup()
        {           
            // Arrange
            // Setup minimal mock data so Warmup service succeeds
            _factory.MockHttpHandler.Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync",
                   ItExpr.Is<HttpRequestMessage>(r => r.RequestUri.ToString().Contains("beststories.json")),
                   ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new HttpResponseMessage
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent("[1]")
               });

            _factory.MockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.RequestUri.ToString().Contains("item/1.json")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(@"{ ""id"": 1, ""title"": ""Test"" }")
                });

            var client = _factory.CreateClient();

            // Act
            // Wait a bit for the BackgroundService (WarmUp) to execute
            HttpResponseMessage response = null;
            string content = string.Empty;

            for (int i = 0; i < 5; i++) // Retry up to 5 times
            {
                response = await client.GetAsync("/health");
                await Task.Delay(1000);

                content = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == HttpStatusCode.OK && content.Contains("Healthy"))
                    break;
            }

            // #ASSERT
            // If it's still 503, the 'content' will help identify why.
            response.StatusCode.Should().Be(HttpStatusCode.OK, because: $"Final health check failed with: {content}");
            content.Should().Contain("Healthy");
        }
    }
}