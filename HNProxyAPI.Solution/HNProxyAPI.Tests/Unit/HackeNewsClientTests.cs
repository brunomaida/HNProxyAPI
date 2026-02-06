using System.Net;
using FluentAssertions;
using HNProxyAPI.Data;
using HNProxyAPI.Services;
using HNProxyAPI.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Diagnostics.Metrics;

namespace HNProxyAPI.Tests.Unit
{
    public class HackerNewsClientTests
    {
        private readonly Mock<IOptionsMonitor<HackerNewsServiceSettings>> _mockSettings;
        private readonly Mock<IMeterFactory> _mockMeterFactory;
        private readonly Mock<ILogger<HackerNewsClient>> _mockLogger;

        public HackerNewsClientTests()
        {
            // Setup de Settings
            _mockSettings = new Mock<IOptionsMonitor<HackerNewsServiceSettings>>();
            _mockSettings.Setup(x => x.CurrentValue).Returns(new HackerNewsServiceSettings
            {
                UrlBase = "https://test.com/beststories.json",
                UrlBaseStoryById = "https://test.com/item/{0}.json"
            });

            _mockLogger = new Mock<ILogger<HackerNewsClient>>();

            _mockMeterFactory = new Mock<IMeterFactory>();
            _mockMeterFactory
                .Setup(f => f.Create(It.IsAny<MeterOptions>()))
                .Returns((MeterOptions options) => new Meter(options.Name, options.Version, options.Tags));

        }

        // Helper para criar o HttpClient com uma resposta pré-definida
        private HackerNewsClient CreateSut(HttpResponseMessage responseMessage)
        {
            var handlerMock = new Mock<HttpMessageHandler>();

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(responseMessage);

            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("https://test.com/")
            };

            return new HackerNewsClient(httpClient, _mockLogger.Object, _mockSettings.Object, _mockMeterFactory.Object);
        }

        // Helper para criar o Sut que lança exceção (simular timeout/erro de rede)
        private HackerNewsClient CreateSutThatThrows(Exception exceptionToThrow)
        {
            var handlerMock = new Mock<HttpMessageHandler>();

            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ThrowsAsync(exceptionToThrow);

            var httpClient = new HttpClient(handlerMock.Object) { BaseAddress = new Uri("https://test.com/") };
            return new HackerNewsClient(httpClient, _mockLogger.Object, _mockSettings.Object, _mockMeterFactory.Object);
        }

        [Fact]
        public async Task GetCurrentStoriesAsync_Should_Return_Ids_On_Success()
        {
            // Arrange
            var jsonResponse = "[10, 20, 30, 40]";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse)
            };
            var sut = CreateSut(response);

            // Act
            var result = await sut.GetCurrentStoriesAsync(CancellationToken.None);

            // #ASSERT
            result.Should().NotBeNull();
            result.Should().HaveCount(4);
            result.Should().ContainInOrder(10, 20, 30, 40);
        }

        [Fact]
        public async Task GetCurrentStoriesAsync_Should_Return_EmptyArray_On_HttpError()
        {
            // Arrange
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError); // 500 Error
            var sut = CreateSut(response);

            // Act
            var result = await sut.GetCurrentStoriesAsync(CancellationToken.None);

            // #ASSERT
            result.Should().BeEmpty();
            // Verifica se logou erro
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error retrieving")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetCurrentStoriesAsync_Should_Return_EmptyArray_On_Cancellation()
        {
            // Arrange
            var sut = CreateSutThatThrows(new OperationCanceledException());

            // Act
            var result = await sut.GetCurrentStoriesAsync(CancellationToken.None);

            // #ASSERT
            result.Should().BeEmpty();
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("timeout/cancelled")),
                    It.IsAny<OperationCanceledException>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetStoryDetailsAsync_Should_Return_Story_On_Success()
        {
            var jsonResponse = """
            {
                "id": 12345,
                "title": "Test Story",
                "score": 100,
                "by": "tester",
                "time": 1600000000,
                "url": "http://google.com"
            }
            """;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse)
            };
            var sut = CreateSut(response);
            var result = await sut.GetStoryDetailsAsync(12345, CancellationToken.None);

            // #ASSERT
            result.id.Should().Be(12345);
            result.title.Should().Be("Test Story");
            // Se o StoryConverter estiver funcionando corretamente, ele mapeia "url" para "uri" na struct
            // result.uri.Should().Be("http://google.com"); 
        }

        [Fact]
        public async Task GetStoryDetailsAsync_Should_Return_Default_On_HttpError()
        {
            // Arrange
            var response = new HttpResponseMessage(HttpStatusCode.NotFound); // 404
            var sut = CreateSut(response);

            // Act
            var result = await sut.GetStoryDetailsAsync(999, CancellationToken.None);

            // #ASSERT
            result.Should().Be(default(Story));
            result.id.Should().Be(0);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error retrieving Story")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetStoryDetailsAsync_Should_Return_Default_On_Cancellation()
        {
            // Arrange
            var sut = CreateSutThatThrows(new OperationCanceledException());

            // Act
            var result = await sut.GetStoryDetailsAsync(888, CancellationToken.None);

            // #ASSERT
            result.Should().Be(default(Story));
            result.id.Should().Be(0);

            _mockLogger.Verify(
               x => x.Log(
                   LogLevel.Warning,
                   It.IsAny<EventId>(),
                   It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("timeout/cancelled")),
                   It.IsAny<OperationCanceledException>(),
                   It.IsAny<Func<It.IsAnyType, Exception, string>>()),
               Times.Once);
        }
    }
}
