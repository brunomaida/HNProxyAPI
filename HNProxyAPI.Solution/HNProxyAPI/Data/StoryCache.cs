using HNProxyAPI.Settings;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.Metrics;

namespace HNProxyAPI.Data
{
    /// <summary>
    /// Controls the current Stories in/out the hot/path, always ordered
    /// </summary>
    public class StoryCache : IStoryCache, IDisposable
    {
        private readonly ILogger<IStoryCache> _logger;
        private readonly IOptionsMonitor<HackerNewsServiceSettings> _settings;

        // Local O(1) dictionary to ensure uniqueness
        private readonly ConcurrentDictionary<int, Story> _storyCache = new();    
        
        // #MT: Allows multiple coordinated read-write among threads
        private volatile IReadOnlyList<Story> _orderedList = ImmutableList<Story>.Empty; 

        // Concurrency Control (one at a time)
        private readonly SemaphoreSlim _semCacheLock = new(1, 1);

        // State Control (Atomic)
        private long _currentMemoryUsageBytes = 0;
        private long _averageObjectSizeBytes = 0;

        // Metrics
        private readonly Meter _meter;
        private readonly ObservableGauge<long> _memoryGauge;
        private readonly ObservableGauge<int> _countGauge;
        private readonly ObservableGauge<long> _avgSizeGauge;

        // Static comparator to avoid delegate allocation during sorting
        private static readonly Comparison<Story> ScoreComparison = (a, b) => b.score.CompareTo(a.score);

        /// <summary>
        /// Creates an instance to control Stories (add, remove and reorder).
        /// </summary>
        /// <param name="logger">Injected logger</param>
        /// <param name="settings">Injected settings</param>
        /// <param name="meterFactory">Injected meter factory for statistics</param>
        public StoryCache(
            ILogger<IStoryCache> logger,
            IOptionsMonitor<HackerNewsServiceSettings> settings,
            IMeterFactory meterFactory)
        {
            _logger = logger;
            _settings = settings;

            // Metrics Configuration
            _meter = meterFactory.Create("HNProxyAPI.Cache");

            _memoryGauge = _meter.CreateObservableGauge(
                "broker.cache.bytes",
                () => Interlocked.Read(ref _currentMemoryUsageBytes),
                unit: "By", description: "Estimated memory usage of stories");

            _countGauge = _meter.CreateObservableGauge(
                "broker.cache.count",
                () => _storyCache.Count,
                unit: "{items}", description: "Total stories in cache");

            _avgSizeGauge = _meter.CreateObservableGauge(
                "broker.cache.avg_obj_size",
                () => Interlocked.Read(ref _averageObjectSizeBytes),
                unit: "By", description: "Calculated average size of a story object");
        }

        /// <summary>
        /// State of the cache
        /// </summary>
        /// <returns>Returns if cache (memory) is empty (has no data)</returns>
        public bool IsEmpty
        {
            get { return _storyCache.IsEmpty; }
        }

        /// <summary>
        /// Checks if corresponding ID is in cache
        /// </summary>
        /// <param name="id">Story ID</param>
        /// <returns>If the cache contains the ID</returns>
        public bool Contains(int id)
        {
            return _storyCache.ContainsKey(id);
        }

        /// <summary>
        /// Retrieves the ordered list of Stories in cache
        /// </summary>
        /// <returns>Descending ordered list of Stories</returns>
        public IReadOnlyList<Story> GetOrderedList()
        {
            // Returns the reference to the current snapshot (Zero CPU, Zero Lock)
            return _orderedList;
        }

        /// <summary>
        /// Tries to add a new Story into the cache
        /// </summary>
        /// <param name="story">Current story to add</param>
        /// <returns>If the operation succeeds</returns>
        public bool TryAdd(Story story)
        {
            // Memory Limit Check (Heuristic)
            long avgSize = Interlocked.Read(ref _averageObjectSizeBytes);
            avgSize = (avgSize == 0 ? _settings.CurrentValue.AverageObjectSizeBytes : avgSize);

            long currentUsage = Interlocked.Read(ref _currentMemoryUsageBytes);
            long limit = _settings.CurrentValue.MaxMemoryThresholdBytes;

            // If adding this item (based on average) exceeds the limit, reject it.
            if (currentUsage + avgSize > limit)
            {
                _logger.LogWarning("*** Memory Full: will not add more Stories in memory list.");
                return false; // Memory full
            }

            // Attempt Addition
            if (_storyCache.TryAdd(story.id, story))
            {
                // Increments projected memory usage
                Interlocked.Add(ref _currentMemoryUsageBytes, avgSize);
                _logger.LogInformation("[memory] Added Story({id}) to local cache.", story.id);
                return true;
            }
            else _logger.LogError("[memory] Story({id}) NOT added to local cache.", story.id);

            return true; // Already existed, considered success
        }

        /// <summary>
        /// Removes the listed IDs from the cache
        /// </summary>
        /// <param name="oldIds">List of Story IDs</param>
        public void RemoveOldIds(IEnumerable<int> oldIds)
        {
            if (!oldIds.Any()) return;
            long avgSize = Interlocked.Read(ref _averageObjectSizeBytes);
            int removed = 0;

            foreach (int id in oldIds)
            {
                // attempt item removall
                if (_storyCache.TryRemove(id, out _))
                {
                    Interlocked.Add(ref _currentMemoryUsageBytes, -avgSize);
                    removed++;
                }
                else
                {
                    _logger.LogError("[memory] Story({id}) NOT removed from local cache.", id);
                }
            }
            _logger.LogInformation("[memory] Removed total of {count} IDs from Story cache.", removed);
        }

        /// <summary>
        /// Asynchronosly rebuilds the list in desceding order
        /// </summary>
        /// <returns>The process to execute the reorder mechanism</returns>
        public async Task RebuildOrderedListAsync()
        {
            // Locks to ensure only 1 task reorders at a time
            await _semCacheLock.WaitAsync();
            try
            {
                // Snapshot of current values
                var storiesArray = _storyCache.Values.ToArray();

                if (storiesArray.Length == 0) return;

                // Sorting (CPU Bound)
                Array.Sort(storiesArray, ScoreComparison);

                // Atomic List Swap
                _orderedList = ImmutableArray.Create(storiesArray);

                // Average Recalibration (Logarithmic Sampling)
                RecalculateAverageSize(storiesArray);

                _logger.LogInformation(
                    "Ordered List of Stories rebuilt. Items: {Count}. Memory: {Bytes} bytes.",
                    storiesArray.Length, Interlocked.Read(ref _currentMemoryUsageBytes));
                
            }
            finally
            {
                _semCacheLock.Release();
            }
        }

        /// <summary>
        /// Recalculates the average object size using Sqrt sampling 
        /// </summary>
        /// <param name="stories">The array-list of Stories</param>
        private void RecalculateAverageSize(Story[] stories)
        {
            int totalCount = stories.Length;
            if (totalCount == 0) return;

            // Rule: SQRT not so agressive as 1/10 ou 1/20 of the total and not so little as Log10(n)
            int sampleSize = (int)Math.Ceiling(Math.Sqrt(totalCount));
            long totalSampleBytes = 0;

            // Take the first N items (assuming uniform size distribution)
            for (int i = 0; i < totalCount; i += sampleSize)
            {
                // Use static Helper to measure
                totalSampleBytes += StoryMetrics.EstimateMemorySizeOf(in stories[i]);
            }

            long newAverage = totalSampleBytes / sampleSize;

            // Atomic average update
            long oldAverage = Interlocked.Exchange(ref _averageObjectSizeBytes, newAverage);

            // Fine-tuning: Recalculates total memory based on the new average.
            // This corrects accumulated deviations over time.
            long newTotalMemory = totalCount * newAverage;
            Interlocked.Exchange(ref _currentMemoryUsageBytes, newTotalMemory);

            _logger.LogDebug("[memory] Metrics updated. Avg Size: {Old} -> {New} bytes.", oldAverage, newAverage);
        }

        /// <summary>
        /// Disposes the object releasing its internal resources
        /// </summary>
        public void Dispose()
        {
            _meter?.Dispose();
            _semCacheLock?.Dispose();
        }
    }
}
