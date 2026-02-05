using BenchmarkDotNet.Attributes;
using HNProxyAPI.Data;
using HNProxyAPI.Services;
using HNProxyAPI.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Diagnostics.Metrics;

namespace HNProxyAPI.Benchmark
{
    [MemoryDiagnoser] // allocated bytes and Garbage Collection (Gen 0/1/2)
    [ThreadingDiagnoser] // Lock Contention (SemaphoreSlim)
    public class SystemBenchmark
    {
        private HackerNewsQueryService _service;
        private StoryCache _realCache; // Real cache to measure actual memory impact
        private Mock<IHackerNewsClient> _mockClient;
        private Mock<IMeterFactory> _mockMeterFactory;
        private Mock<ILogger<IStoryCache>> _mockLogger;
        private Mock<IOptionsMonitor<HackerNewsServiceSettings>> _mockSettings;

        // 1. SCALABILITY: Number of records to process
        [Params(100, 1_000, 10_000)]
        public int RecordCount;

        // 2. CONCURRENCY: Number of parallel threads accessing the service
        [Params(1, 50)]
        public int ConcurrentReaders;

        [GlobalSetup]
        public void Setup()
        {
            // 1. Configure Settings (MaxConcurrentRequests affects Parallel.ForEachAsync)
            _mockSettings = new Mock<IOptionsMonitor<HackerNewsServiceSettings>>();
            _mockSettings.Setup(s => s.CurrentValue).Returns(new HackerNewsServiceSettings
                {
                    MaxMemoryThresholdBytes = 500L * 1024 * 1024, // 500MB
                    MaxConcurrentRequests = 5 // Simulates a real restriction
                });

            _mockLogger = new Mock<ILogger<IStoryCache>>();

            _mockMeterFactory = new Mock<IMeterFactory>();
            _mockMeterFactory
                .Setup(f => f.Create(It.IsAny<MeterOptions>()))
                .Returns((MeterOptions options) => new Meter(options.Name, options.Version, options.Tags));

            // 2. Configure Cache (Real implementation to test integration)
            _realCache = new StoryCache(
                _mockLogger.Object,
                _mockSettings.Object,
                _mockMeterFactory.Object
            );

            // 3. Configure Client Mock
            _mockClient = new Mock<IHackerNewsClient>();

            // Setup: Returns a list of IDs based on RecordCount
            var ids = Enumerable.Range(1, RecordCount).ToArray();
            _mockClient.Setup(c => c.GetCurrentStoriesAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(ids);

            // Setup: Returns dummy details for any ID
            _mockClient.Setup(c => c.GetStoryDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync((int id, CancellationToken ct) => new Story
                       {
                           id = id,
                           title = $"Benchmark Story {id}",
                           score = 100
                       });

            // 4. Instantiate the System Under Test (SUT)
            _service = new HackerNewsQueryService(
                _mockClient.Object,
                _realCache,
                _mockSettings.Object,
                Mock.Of<ILogger<HackerNewsQueryService>>(),
                _mockMeterFactory.Object
            );

            // Pre-warm the cache for "Hot Path" scenarios
            // We force a run so the cache is populated before benchmarks start
            _service.GetBestStoriesOrderedByScoreAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        // SCENARIO A: Hot Path (Cache Hit)
        // Tests the logic when IDs haven't changed.
        // Expectation: Extremely fast, zero locking contention after the first check.
        [Benchmark]
        public async Task HotPath_NoUpdates()
        {
            // The client returns the SAME IDs setup in GlobalSetup.
            // The service should detect "No new IDs" and return immediately.
            await _service.GetBestStoriesOrderedByScoreAsync(CancellationToken.None);
        }

        // SCENARIO B: Cold Path / Refresh (Cache Miss)
        // Tests the Parallel.ForEachAsync and SemaphoreSlim contention.
        // We simulate a scenario where the API returns completely NEW IDs.
        [Benchmark]
        public async Task ColdPath_FullUpdate()
        {
            // We change the mock behavior to return NEW IDs for this run
            // forcing the service to enter the "Parallel.ForEachAsync" block.
            var startId = RecordCount + 1000;
            var newIds = Enumerable.Range(startId, RecordCount).ToArray();

            _mockClient.Setup(c => c.GetCurrentStoriesAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(newIds);

            await _service.GetBestStoriesOrderedByScoreAsync(CancellationToken.None);
        }

        // SCENARIO C: Concurrency Stress
        // Simulates multiple users requesting data while the system might be updating.
        // Validates the SemaphoreSlim behavior (WaitAsync(0)).
        [Benchmark]
        public async Task ConcurrentAccess_Stress()
        {
            var tasks = new Task[ConcurrentReaders];
            for (int i = 0; i < ConcurrentReaders; i++)
            {
                tasks[i] = Task.Run(() => _service.GetBestStoriesOrderedByScoreAsync(CancellationToken.None));
            }
            await Task.WhenAll(tasks);
        }
    }
}