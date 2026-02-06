using FluentAssertions;
using HNProxyAPI.Settings;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;

namespace HNProxyAPI.Tests.Unit
{
    [Collection("Sequential Tests")]
    public class MetricsRequestHandlerTests
    {
        private SemaphoreSlim _semExec = new SemaphoreSlim(1,1);
        private const string METER_NAME = "Network.MetricsRequestHandler";
        private readonly Mock<IOptionsMonitor<HackerNewsServiceSettings>> _mockSettings;
        private readonly IMeterFactory _meterFactory;
        private readonly ServiceProvider _serviceProvider;
        public MetricsRequestHandlerTests()
        {
            _mockSettings = new Mock<IOptionsMonitor<HackerNewsServiceSettings>>();

            var services = new ServiceCollection();
            services.AddMetrics(); // Habilita o sistema de métricas no DI
            _serviceProvider = services.BuildServiceProvider();
            _meterFactory = _serviceProvider.GetRequiredService<IMeterFactory>();
        }

        [Fact]
        public async Task SendAsync_Should_Handle_Generic_Exceptions()
        {
            // Arrange
            var (handler, innerMock) = CreateSystemUnderTest();

            // Simulates DNS or Network error
            innerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("DNS Error"));

            var invoker = new HttpMessageInvoker(handler);
            using var counterCollector = new MetricCollector<long>(null, METER_NAME, "http.client.requests_total");

            // Act
            var action = async () => await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"), CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<HttpRequestException>();

            var metric = counterCollector.LastMeasurement;
            metric.Tags["error_type"].Should().Be("HttpRequestException");
        }

        [Fact]
        public async Task SendAsync_Should_Record_Success_Metrics()
        {
            // Arrange
            var (handler, innerMock) = CreateSystemUnderTest();

            // Simulates 200 OK response
            innerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            var invoker = new HttpMessageInvoker(handler);

            // Metric Collectors (Listen to the Meter created inside the class)
            using var counterCollector = new MetricCollector<long>(null, METER_NAME, "http.client.requests_total");
            using var activeCollector = new MetricCollector<long>(null, METER_NAME, "http.client.active_requests");

            // Act
            await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"), CancellationToken.None);

            // Assert
            // 1. Total Counter should be 1
            var lastCounter = counterCollector.LastMeasurement;
            lastCounter.Value.Should().Be(1);
            lastCounter.Tags["status_code"].Should().Be(200);
            lastCounter.Tags["host"].Should().Be("test.com");

            // 2. Active Counter should have gone up (+1) and down (-1), ending with zero delta?
            // MetricCollector records every change. We should have a +1 record and a -1 record.

            // FIX: Corrected method name from HaveCountGreaterOrEqualTo to HaveCountGreaterThanOrEqualTo
            activeCollector.GetMeasurementSnapshot().Should().HaveCountGreaterThanOrEqualTo(2);
            activeCollector.GetMeasurementSnapshot().Last().Value.Should().Be(-1); // UpDownCounter records the delta; the code performs Add(-1)
        }

        [Fact]
        public async Task SendAsync_Should_Throttle_Concurrent_Requests()
        {
            // Arrange
            // LIMITED TO 1 CONCURRENT REQUEST
            var (handler, innerMock) = CreateSystemUnderTest(maxConcurrent: 1);

            // Use TaskCompletionSource to manually control when requests finish
            var tcs1 = new TaskCompletionSource<bool>();
            var tcs2 = new TaskCompletionSource<bool>();

            // Configure Mock to wait for signal before returning
            innerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns(async (HttpRequestMessage req, CancellationToken ct) =>
                {
                    if (req.RequestUri.ToString().Contains("req1"))
                        await tcs1.Task; // Req 1 halts here
                    else
                        await tcs2.Task; // Req 2 halts here

                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

            var invoker = new HttpMessageInvoker(handler);
            using var queueHistogram = new MetricCollector<double>(null, METER_NAME, "http.client.avg_queue_duration");

            // Act

            // 1. Trigger Request 1 (Will acquire Semaphore and halt at TCS1)
            var task1 = invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com/req1"), CancellationToken.None);

            // Short delay to ensure Task 1 acquired the semaphore
            await Task.Delay(50);

            // 2. Trigger Request 2 (Should get stuck at Semaphore, DOES NOT EVEN REACH inner handler yet)
            var sw = Stopwatch.StartNew();
            var task2 = invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com/req2"), CancellationToken.None);

            // 3. Verify if Task 2 is pending (blocked)
            task2.IsCompleted.Should().BeFalse();

            // 4. Release Task 1
            tcs1.SetResult(true);
            await task1; // Finish Req 1, release semaphore

            // 5. Now Task 2 can enter. Release it.
            tcs2.SetResult(true);
            await task2;
            sw.Stop();

            // Assert
            // Verify if queue metric recorded anything (at least one request waited or passed through)
            var measurements = queueHistogram.GetMeasurementSnapshot();
            measurements.Should().NotBeEmpty();

            // The second request must have waited a significant time (greater than the 50ms delay we put)
            // This proves SemaphoreSlim worked.
            // Note: Exact time tests are fragile, we only check if it was not zero.
            measurements.Should().Contain(m => m.Value > 0);
        }

        [Fact]
        public async Task SendAsync_Should_Throw_And_Record_Timeout_Metrics()
        {
            // Arrange
            int timeoutMs = 50;
            var (handler, innerMock) = CreateSystemUnderTest(timeoutMs: timeoutMs);

            // Simulates a delay LONGER than the timeout (200ms > 50ms)
            innerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Returns(async (HttpRequestMessage request, CancellationToken token) =>
                {
                    await Task.Delay(200, token); // Will be cancelled before finishing
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });

            var invoker = new HttpMessageInvoker(handler);
            using var counterCollector = new MetricCollector<long>(null, METER_NAME, "http.client.requests_total");

            // Act
            var action = async () => await invoker.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://test.com"), CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<OperationCanceledException>();

            var metric = counterCollector.LastMeasurement;
            metric.Value.Should().Be(1);
            metric.Tags["error_type"].Should().Be("TimeoutException"); // Defined in your catch block
            metric.Tags["status_code"].Should().Be(0);
        }

        // Helper to create the pipeline (Handler under test + Mocked Inner Handler)
        private (MetricsRequestHandler handler, Mock<HttpMessageHandler> innerHandlerMock) CreateSystemUnderTest(
            int maxConcurrent = 10,
            int timeoutMs = 3000)
        {
            // Configure settings
            _mockSettings.Setup(x => x.CurrentValue).Returns(new HackerNewsServiceSettings
            {
                MaxConcurrentRequests = maxConcurrent,
                RequestTimeoutMs = timeoutMs
            });

            var handler = new MetricsRequestHandler(_mockSettings.Object, _meterFactory);
            var innerHandlerMock = new Mock<HttpMessageHandler>();

            // Connects the handler under test to the inner mock
            handler.InnerHandler = innerHandlerMock.Object;

            return (handler, innerHandlerMock);
        }
    }
}