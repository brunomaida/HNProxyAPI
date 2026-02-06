using HNProxyAPI.Data;
using HNProxyAPI.Extensions;
using HNProxyAPI.Settings;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HNProxyAPI.Services
{
    /// <summary>
    /// The orchestrator responsible to query HackerNews info, store data in memory e delivery the final result.    
    /// </summary>
    public class HackerNewsQueryService
    {
        private const string METER_NAME = "Network.HackerNewsQueryService";

        private int _requestCounter = 0;
        private readonly SemaphoreSlim _semGate = new SemaphoreSlim(1, 1);
        private readonly IHackerNewsClient _hackerNewsClient;
        private readonly IStoryCache _storyCache;
        private readonly ILogger<HackerNewsQueryService> _logger;
        private readonly IOptionsMonitor<HackerNewsServiceSettings> _settings;
        private readonly Meter _meter;
        private readonly Histogram<double> _queryTimeHistorgram;

        public HackerNewsQueryService(IHackerNewsClient client,
                                      IStoryCache cache,
                                      IOptionsMonitor<HackerNewsServiceSettings> settings,
                                      ILogger<HackerNewsQueryService> logger, 
                                      IMeterFactory meterFactory)
        {
            _hackerNewsClient = client;
            _storyCache = cache;
            _settings = settings;
            _logger = logger;
            _meter = meterFactory.Create(METER_NAME);
            _queryTimeHistorgram = _meter.CreateHistogram<double>(
                "hn.service.query.request_time",
                unit: "ms",
                description: "Avg time to query all Story List details"
                );
        }

        /// <summary>
        /// 
        /// Info: Virtual attribute used for test purposes (mock needs it to be able to intercept this method)
        /// </summary>
        /// <param name="ct">The Cancellation token used in the entire process.</param>
        /// <returns>The desceding ordered list of Stories according to their score.</returns>
        public virtual async Task<IEnumerable<Story>> GetBestStoriesOrderedByScoreAsync(CancellationToken ct)
        {
            
            bool isLocked = false;

            // #MT: Conditional locking strategy
            // If the cache is empty, block indefinitely to ensure data initialization.
            // If data exists, attempt a non-blocking lock (try-acquire) to skip redundant updates if another thread is already working.
            if (_storyCache.IsEmpty)
            {
                await _semGate.WaitAsync(ct);
                isLocked = true;
            }
            else
            {
                isLocked = await _semGate.WaitAsync(0, ct);
                if (!isLocked)
                {
                    _logger.LogInformation("[memory] Update in progress in another Task.");
                }
            }

            using (_logger.BeginScope("[REQ:{n}]", ++_requestCounter))
            {
                try
                {
                    var currentList = _storyCache.GetOrderedList();

                    var currentIds = await _hackerNewsClient.GetCurrentStoriesAsync(ct);
                    if (currentIds.Length == 0)
                        return currentList;

                    var cachedIds = currentList.Select(s => s.id).ToHashSet();
                    var (idsToAdd, idsToDel) = StoryDifferences.GetDelta(currentIds, cachedIds);
                    _storyCache.RemoveOldIds(idsToDel);

                    if (idsToAdd.Count == 0)
                    {
                        _logger.LogInformation("No new IDs retrieved. Will use the ones in memory (already ordered)");
                        return _storyCache.GetOrderedList();
                    }                    

                    long start = Stopwatch.GetTimestamp();

                    await Parallel.ForEachAsync(idsToAdd,
                        new ParallelOptions
                        {
                            MaxDegreeOfParallelism = _settings.CurrentValue.MaxConcurrentRequests,
                            CancellationToken = ct
                        },
                        async (id, ctoken) =>
                        {
                            var story = await _hackerNewsClient.GetStoryDetailsAsync(id, ctoken);
                            if (story.id > 0)
                            {
                                if (!_storyCache.TryAdd(story))
                                {
                                    _logger.LogWarning("Cache full, skipping story {Id}", id);
                                }
                                else
                                {
                                    _logger.LogDebug(story.ToString());
                                }
                            }
                        });

                    double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    _queryTimeHistorgram.Record(elapsed);
                    _logger.LogDebug("-> Timespan to query NEW Story details in Hacker News: {TimeInMs}ms", elapsed);

                    await _storyCache.RebuildOrderedListAsync();

                    return _storyCache.GetOrderedList();
                }
                finally
                {
                    if (isLocked) _semGate.Release();
                }
            }
        }
    }
}
