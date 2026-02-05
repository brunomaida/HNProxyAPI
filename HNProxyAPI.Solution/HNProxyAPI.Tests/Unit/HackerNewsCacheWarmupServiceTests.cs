using HNProxyAPI.Data;
using HNProxyAPI.Services;
using HNProxyAPI.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Diagnostics.Metrics;

namespace HNProxyAPI.Tests.Unit
{
    public class HackerNewsCacheWarmUpServiceTests
    {
        private readonly Mock<IServiceProvider> _mockRootServiceProvider;
        private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
        private readonly Mock<IServiceScope> _mockScope;
        private readonly Mock<IServiceProvider> _mockScopedServiceProvider;
        private readonly Mock<ILogger<HackerNewsCacheWarmUpService>> _mockLogger;
        private readonly Mock<IOptionsMonitor<HackerNewsServiceSettings>> _mockSettings;
        private readonly Mock<IMeterFactory> _mockScopeMeter;

        // Mocking the concrete class (requires virtual method) or the interface
        private readonly Mock<HackerNewsQueryService> _mockQueryService;

        public HackerNewsCacheWarmUpServiceTests()
        {
            _mockRootServiceProvider = new Mock<IServiceProvider>();
            _mockScopeFactory = new Mock<IServiceScopeFactory>();
            _mockScope = new Mock<IServiceScope>();
            _mockScopedServiceProvider = new Mock<IServiceProvider>();
            _mockLogger = new Mock<ILogger<HackerNewsCacheWarmUpService>>();
            _mockSettings = new Mock<IOptionsMonitor<HackerNewsServiceSettings>>();
            _mockScopeMeter = new Mock<IMeterFactory>();
            
            _mockSettings.Setup(s => s.CurrentValue).Returns(new HackerNewsServiceSettings());

            _mockScopeMeter
                .Setup(f => f.Create(It.IsAny<MeterOptions>()))
                .Returns((MeterOptions options) => new Meter(options.Name, options.Version, options.Tags));

            // Here we are mocking the concrete class. Remember: The GetBestStories... method must be VIRTUAL.
            // If changing the code is not possible, use an IHackerNewsQueryService interface.
            _mockQueryService = new Mock<HackerNewsQueryService>(
                Mock.Of<IHackerNewsClient>(),
                Mock.Of<IStoryCache>(),
                _mockSettings.Object, // Use o mock configurado, não null ou dummy
                Mock.Of<ILogger<HackerNewsQueryService>>(),
                _mockScopeMeter.Object // <--- AQUI: Passa o mock configurado que retorna o Meter real
            );

            // Configure mock scope
            _mockRootServiceProvider
                .Setup(x => x.GetService(typeof(IServiceScopeFactory)))
                .Returns(_mockScopeFactory.Object);

            _mockScopeFactory
                .Setup(x => x.CreateScope())
                .Returns(_mockScope.Object);

            _mockScope
                .Setup(x => x.ServiceProvider)
                .Returns(_mockScopedServiceProvider.Object);

            _mockScopedServiceProvider
                .Setup(x => x.GetService(typeof(HackerNewsQueryService)))
                .Returns(_mockQueryService.Object);
        }

        [Fact]
        public async Task ExecuteAsync_Should_WarmUp_Cache_Successfully()
        {
            var fakeStories = new List<Story>
            {
                new Story { id = 1, title = "Test 1" },
                new Story { id = 2, title = "Test 2" }
            };

            // Configure the service to return fake data
            _mockQueryService
                .Setup(s => s.GetBestStoriesOrderedByScoreAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(fakeStories);

            var service = new HackerNewsCacheWarmUpService(
                _mockRootServiceProvider.Object,
                _mockLogger.Object);

            // BackgroundService exposes StartAsync, which calls ExecuteAsync internally
            await service.StartAsync(CancellationToken.None);

            // Allow the background service (WarmUp) to finish processing
            await Task.Delay(500);

            // Verify if the load method was called
            _mockQueryService.Verify(
                s => s.GetBestStoriesOrderedByScoreAsync(It.IsAny<CancellationToken>()),
                Times.Once);

            // Verify if success was logged (Information)
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Cache warm up completed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_Should_Log_Critical_Error_On_Exception()
        {
            // Arrange
            var expectedEx = new Exception("API is down!");

            // Configure the service to fail
            _mockQueryService
                .Setup(s => s.GetBestStoriesOrderedByScoreAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedEx);

            var service = new HackerNewsCacheWarmUpService(
                _mockRootServiceProvider.Object,
                _mockLogger.Object);

            // StartAsync should not throw the exception, as BackgroundService catches exceptions in some implementations,
            // BUT in this code there is an explicit try/catch inside ExecuteAsync.
            await service.StartAsync(CancellationToken.None);

            // Allow the background service (WarmUp) to finish processing
            await Task.Delay(500);
            // Assert

            // Verify if CRITICAL was logged with the correct exception
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Critical,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Failed to wamp up")),
                    expectedEx, // Verify if the passed exception is the one we generated
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}