using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using HNProxyAPI.Data;
using HNProxyAPI.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Diagnostics.Metrics;

namespace HNProxyAPI.Benchmark
{
    // Global Benchmark Configurations
    [MemoryDiagnoser] // Essential: Shows allocated Bytes and GC collections (Gen 0, 1, 2)
    [RankColumn]      // Displays a ranking from fastest to slowest
    [SimpleJob(RuntimeMoniker.Net80)] // Ensures execution on .NET 8
    public class StoryCachePerformanceBenchmark
    {
        private StoryCache _cache;
        private Story[] _dataset;

        // Load Variation: From small (1k) to massive (1 Million)
        // This validates if the sorting algorithm scales linearly or exponentially
        [Params(1_000, 10_000, 100_000, 1_000_000)]
        public int ItemCount;

        [GlobalSetup]
        public void Setup()
        {
            // 1. Configuration Mock (Allowing unlimited memory so the test doesn't fail)
            var mockSettings = new Mock<IOptionsMonitor<HackerNewsServiceSettings>>();
            mockSettings.Setup(s => s.CurrentValue).Returns(new HackerNewsServiceSettings
            {
                MaxMemoryThresholdBytes = long.MaxValue, // No limits
                AverageObjectSizeBytes = 256
            });

            // 2. Lightweight Dependency Mocks
            var mockLogger = new Mock<ILogger<StoryCache>>();
            var mockMeterFactory = new Mock<System.Diagnostics.Metrics.IMeterFactory>();

            // Meter Factory setup to prevent constructor failure
            mockMeterFactory.Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<KeyValuePair<string, object>>>()))
                            .Returns(new System.Diagnostics.Metrics.Meter("BenchmarkMeter"));

            _cache = new StoryCache(mockLogger.Object, mockSettings.Object, mockMeterFactory.Object);

            // 3. Random Data Generation (To force the Sort algorithm to work)
            var rnd = new Random(42); // Fixed seed for reproducibility
            _dataset = new Story[ItemCount];

            for (int i = 0; i < ItemCount; i++)
            {
                var story = new Story
                {
                    id = i,
                    title = $"Benchmark Story Title {i} - performance test",
                    score = rnd.Next(0, 50000), // Random scores to sort
                    postedBy = "user_benchmark",
                    time = DateTime.UtcNow
                };

                // Pre-populate the cache
                _cache.TryAdd(story);
            }
        }

        // --- SCENARIO 1: Sorting + Snapshot Cost ---
        // Measures time taken to sort the list and perform the reference swap
        // Measures temporary memory allocation during the process (auxiliary arrays)
        [Benchmark]
        public async Task RebuildSnapshot_SortingTime()
        {
            await _cache.RebuildOrderedListAsync();
        }

        // --- SCENARIO 2: Insertion Cost (Dictionary Overhead) ---
        // Measures ConcurrentDictionary efficiency and atomic metrics (Interlocked)
        // Note: We test adding items to measure real insertion overhead
        [Benchmark]
        public void InsertSingleItem_MemoryOverhead()
        {
            // Fixed ID outside Setup range to ensure actual insertion
            var newStory = new Story { id = ItemCount + 1, score = 999, title = "New Item" };

            _cache.TryAdd(newStory);
        }

        // --- SCENARIO 3: Reading (Baseline) ---
        // Proves that reading is O(1) and Zero Allocation
        [Benchmark]
        public IReadOnlyList<Story> ReadSnapshot_AccessTime()
        {
            return _cache.GetOrderedList();
        }
    }
}