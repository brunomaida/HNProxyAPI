/*
using HNProxyAPI.Data;
using HNProxyAPI.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;

namespace HNProxyAPI.Services
{
    public class HackerNewsQueryService : IDisposable
    {     
        private readonly HttpClient _client;                        // HttpClient instance for making HTTP requests
        private readonly ILogger<HackerNewsQueryService> _logger;   // Logger instance for logging

        // Metrics objects
        private readonly Meter _meter;                          // Meter for metrics collection
        private readonly ObservableGauge<long> _memoryGauge;    // Gauge for tracking memory usage
        private readonly ObservableGauge<int> _itemCountGauge;  // Gauge for tracking item count in cache
        private readonly Histogram<double> _queryTimeHistorgram;// Histogram for tracking average query time of all new Ids

        // Settings retrieved from local file appconfig.json
        private readonly IOptionsMonitor<HackerNewsServiceSettings> _serviceSettings;
        private readonly IOptionsMonitor<MemoryMetricsSettings> _memoryMetricsSettings;

        // Memory objects and lock control to order listed stories
        private ConcurrentDictionary<int, Story> _memoryCache = new();                              // Collection to track current stories being processed
        private volatile IReadOnlyList<Story> _cachedOrderedStories = ImmutableList<Story>.Empty;   // Cached ordered stories for quick retrieval
        private readonly SemaphoreSlim _semCacheLock = new(1, 1);                                   // Semaphore for synchronizing access to the cache

        // Local control variables
        private static long _memoryUsageBytes = 0;              // Current memory usage in bytes 
        private static long _avgObjSizeBytes = 0;

        private static readonly Comparison<Story> ScoreComparison = (a, b) => b.score.CompareTo(a.score);

        public HackerNewsQueryService(ILogger<HackerNewsQueryService> logger,
                                      IHttpClientFactory clientFactory,
                                      IOptionsMonitor<HackerNewsServiceSettings> extAPISettings,
                                      IOptionsMonitor<MemoryMetricsSettings> metricsSettings,
                                      IMeterFactory meterFactory)
        {
            _logger = logger;
            _serviceSettings = extAPISettings;
            _memoryMetricsSettings = metricsSettings;
            _client = clientFactory.CreateClient(_serviceSettings.CurrentValue.HttpClientName);
            _meter = meterFactory.Create(_memoryMetricsSettings.CurrentValue.MetricsMemoryName, _memoryMetricsSettings.CurrentValue.MetricsMemoryVersion);

            _memoryGauge = _meter.CreateObservableGauge(
                "hn.service.memory.usage_bytes",
                observeValue: () => Interlocked.Read(ref _memoryUsageBytes),
                unit: "bytes",
                description: "Current memory usage in bytes for cached stories.");

            _itemCountGauge = _meter.CreateObservableGauge(
                "hn.service.cache.item_count",
                observeValue: () => _memoryCache.Count,
                unit: "{items}",
                description: "Number of stories currently cached in memory.");

            _queryTimeHistorgram = _meter.CreateHistogram<double>(
                "hn.service.request.time",
                unit: "ms",
                description: "Avg time to query all details of new IDs in Hacker news"
                );
        }

        //==================================================================================================
        // #OLD: LINQ BASED IMPLEMENTATION
        // performs the ordering and filtering in a more functional style but does it less efficiently
        // 
        // public async Task<IEnumerable<Story>> GetBestStoriesOrderByScore(bool descending, int firstN)
        // {
        //     var stories = await GetBestStoriesOrderByScore(descending);
        //     stories = descending ? stories.OrderByDescending(s => s.score) : stories.OrderBy(s => s.score);
        //     return stories.Take(firstN);
        // }        
        //==================================================================================================

        public async Task<IEnumerable<Story>> GetBestStoriesOrderedByScoreAsync(CancellationToken ct)
        {

            // Fetch the best story IDs
            int[] ids = await _client.GetFromJsonAsync<int[]>(_serviceSettings.CurrentValue.UrlBase, ct) ?? Array.Empty<int>();

            // Needs to identify new Ids
            var newIds = new List<int>();
            foreach (var id in ids)
                if (!_memoryCache.ContainsKey(id))
                    newIds.Add(id);

            // Returns cached ordered stories if no new Ids : common case after initialization
            if (newIds.Count > 0)
            {
                _logger.LogDebug("Found {Count} new stories to fetch from HackerNews. Will query details...", newIds.Count);
                await QueryNewIdsAsyc(newIds, ct);
                _logger.LogInformation("Sync completed. {Count} new stories were fetched, cached, and ordered.", newIds.Count);
            }
            else
            {
                _logger.LogDebug("No new stories found during sync check.");
            }

            // Returns updated ordered stories
            return _cachedOrderedStories;
        }

        private async Task QueryNewIdsAsyc(List<int> newIds, CancellationToken ct)
        {
            var queryStories = new ConcurrentDictionary<int, Story>();
            long start = Stopwatch.GetTimestamp();

            // #MT: fetch stories in parallel
            await Parallel.ForEachAsync(
                newIds,
                new ParallelOptions { MaxDegreeOfParallelism = _serviceSettings.CurrentValue.MaxConcurrentRequests, CancellationToken = ct },
                async (id, ct) =>
                {
                    try
                    {
                        string url = string.Format(_serviceSettings.CurrentValue.UrlBaseStoryById, id);
                        // #MT: fetch story by ID
                        var newStory = await _client.GetFromJsonAsync<Story>(url, ct);
                        if (newStory.id > 0) // better than newStory != null as this will never be null
                        {
                            //var story = new Story(origStory.title , origStory.url, origStory.by, origStory.time, origStory.score);
                            queryStories.TryAdd(id, newStory);
                        }
                    }
                    catch (OperationCanceledException ex)
                    {
                        _logger.LogWarning(ex, "Fetch cancelled for Story ID {StoryId}.", id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to fetch details for Story ID {StoryId}.", id);
                    }
                });            

            // Measures total duration of querying all new IDs - log and metric purposes (average)
            long durationInMs = Stopwatch.GetElapsedTime(start).Milliseconds;
            _queryTimeHistorgram.Record(durationInMs);
            _logger.LogInformation("-> Timespan to query all new Ids in Hacker News: {TimeInMs}ms", durationInMs);

            if (queryStories.IsEmpty)
            {
                _logger.LogTrace("Batch processing finished but no valid stories were retrieved.");
                return;
            }            

            // Stores the the items in memory
            ProcessMemoryStorage(queryStories);

            // #MT: Rebuild ordered list to be ready in memory
            await RebuildSortedListAsync();
        }

        private void UpdateAverageSize(IReadOnlyList<Story> items)
        {
            int totalItems = items.Count;
            int sampleSize = (int)Math.Ceiling(Math.Sqrt(totalItems));
            long totalSize = 0;
            var list = items.ToImmutableList<Story>();

            for (int i = 0; i < totalItems; i += sampleSize)
            {
                totalSize += EstimateMemorySizeOf(items[i]);
            }

            long newAvgSize = totalSize / sampleSize;

            if (newAvgSize > 0)
            {
                _logger.LogTrace("Recalculated average object size. New Average: {AvgSize} bytes based on sample of {SampleSize} items.", newAvgSize, sampleSize);
            }

            Interlocked.Exchange(ref _avgObjSizeBytes, newAvgSize);
        }

        private void ProcessMemoryStorage(ConcurrentDictionary<int, Story> newStories)
        {
            long avgSize = Interlocked.Read(ref _avgObjSizeBytes);
            avgSize = (avgSize == 0 ? _serviceSettings.CurrentValue.AverageObjectSizeBytes : avgSize);
            long currentUsage = Interlocked.Read(ref _memoryUsageBytes);
            long maxLimit = _serviceSettings.CurrentValue.MaxMemoryThresholdBytes;
            long estimatedBatchSize = newStories.Count * avgSize;

            if (currentUsage + estimatedBatchSize > maxLimit)
            {
                _logger.LogWarning("Memory Overflow Protection: Skipping batch of {BatchCount} items. " +
                                   "Current Usage: {CurrentUsage} bytes. " +
                                   "Estimated Add: {EstimatedAdd} bytes. " +
                                   "Limit: {MaxLimit} bytes.",
                                   newStories.Count, currentUsage, estimatedBatchSize, maxLimit);
                return;
            }

            // Update memory cache
            foreach (var kv in newStories)
            {
                if (_memoryCache.TryAdd(kv.Key, kv.Value))
                {
                    long newMemoryUsage = Interlocked.Add(ref _memoryUsageBytes, avgSize);
                }
            }
        }

        private async Task RebuildSortedListAsync()
        {
            await _semCacheLock.WaitAsync();

            try
            {
                var arrBuffer = _memoryCache.Values.ToArray();
                Array.Sort(arrBuffer, ScoreComparison);
                var sortedList = ImmutableArray.Create(arrBuffer);
                _cachedOrderedStories = sortedList;

                _logger.LogInformation("Snapshot rebuilt successfully. Cache contains {TotalItems} ordered stories.", sortedList.Length);

                // Update memory cache and cached ordered stories under lock
                UpdateAverageSize(_cachedOrderedStories);

                // #OLD: LINQ is easy but costs a lot
                //var sortedList = _rawMemoryCache.Values
                //.OrderByDescending(s => s.Score)
                //.ToImmutableList();
            }
            finally
            {
                _semCacheLock.Release();
            }
        }

        private long EstimateMemorySizeOf(Story story)
        {
            // Overhead constats
            const int ObjectHeaderSize = 24; 
            const int ReferenceSize = 8;     
            const int StringOverhead = 26;  

            long size = ObjectHeaderSize;

            // Strings (C# usa UTF-16, então é char length * 2)
            // Adicionamos o overhead da classe String e o ponteiro para ela
            if (story.title != null)
                size += ReferenceSize + StringOverhead + story.title.Length * 2;

            if (story.uri != null)
                size += ReferenceSize + StringOverhead + story.uri.Length * 2;

            if (story.postedBy != null)
                size += ReferenceSize + StringOverhead + story.postedBy.Length * 2;

            // Value Types (Structs já estão 'inline' na memória do objeto ou registradores, 
            // mas ocupam espaço no layout da classe)
            size += sizeof(long); // Score ou Time (se for long)
            size += sizeof(int);  // Id

            return size;
        }

        public void Dispose()
        {
            _client?.Dispose();
            _meter?.Dispose();
        }
    }
}*/