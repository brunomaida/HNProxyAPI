using FluentAssertions;
using HNProxyAPI.Data;
using HNProxyAPI.Tests.Integration.Setup;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Json;

namespace HNProxyAPI.Tests.Integration
{
    public class BestStoriesEndpointTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public BestStoriesEndpointTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Get_BestStories_Should_Return_200_And_Correct_Json()
        {            
            // Mock the External API responses
            // Step A: Return list of IDs
            _factory.MockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.ToString().Contains("beststories.json")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("[100, 200]")
                });

            // Step B: Return details for ID 100
            _factory.MockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.ToString().Contains("item/100.json")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(@"{ ""id"": 100, ""title"": ""Test Story 100"", ""score"": 50, ""time"": 7282736872  }")
                });

            // Step C: Return details for ID 200
            _factory.MockHttpHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.Is<HttpRequestMessage>(r => r.RequestUri != null && r.RequestUri.ToString().Contains("item/200.json")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(@"{ ""id"": 200, ""title"": ""Test Story 200"", ""score"": 99, ""time"": 7827364634 }")
                });

            var client = _factory.CreateClient();

            HttpResponseMessage? healthResponse = null;
            for (int i = 0; i < 10; i++) 
            {
                healthResponse = await client.GetAsync("/health");
                if (healthResponse.StatusCode == HttpStatusCode.OK) break;
                await Task.Delay(1000);
            }

            healthResponse?.StatusCode.Should().Be(HttpStatusCode.OK, "StoryCache should have been populated.");

            // Act
            var response = await client.GetAsync("/api/beststories?n=2"); 

            // #ASSERT
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var stories = await response.Content.ReadFromJsonAsync<List<Story>>(cancellationToken: TestContext.Current.CancellationToken);
            stories.Should().NotBeNull();
            stories.Should().HaveCountGreaterThanOrEqualTo(2);

            // Verify ordering (Score 99 should come before Score 50)
            stories[0].id.Should().Be(200);
            stories[1].id.Should().Be(100);
        }
    }
}
