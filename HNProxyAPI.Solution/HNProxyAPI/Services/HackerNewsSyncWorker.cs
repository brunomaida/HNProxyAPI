using HNProxyAPI.Data;
using HNProxyAPI.Extensions;
using HNProxyAPI.Settings;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HNProxyAPI.Services
{
    /// <summary>
    /// FUTURE USE: Will use if period updates defined to be the best approach
    /// </summary>
    public class HackerNewsSyncWorker : BackgroundService
    {
        private const string METER_NAME = "Network.HackerNewsSyncWorker";

        private readonly PeriodicTimer _timer;
        private readonly ILogger<HackerNewsSyncWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptionsMonitor<HackerNewsServiceSettings> _settings;
        private readonly Meter _meter;
        private readonly Histogram<double> _queryTimeHistorgram;

        public HackerNewsSyncWorker(ILogger<HackerNewsSyncWorker> logger, 
                                    IServiceProvider serviceProvider, 
                                    IOptionsMonitor<HackerNewsServiceSettings> settings, 
                                    IMeterFactory meterFactory)
        {
            _timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            _logger = logger;
            _serviceProvider = serviceProvider;
            _settings = settings;
            _meter = meterFactory.Create(METER_NAME);
            _queryTimeHistorgram = _meter.CreateHistogram<double>(
                "hn.service.query.request_time",
                unit: "ms",
                description: "Avg time to query all Story List details"
                );

        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[worker] Hacker news sync worker started.");

            while (await _timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DoWorkAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[worker] Error during worker execution");
                }
            }
        }

        private async Task DoWorkAsync(CancellationToken ct)
        {
            using var scope = _serviceProvider.CreateScope();
            var client = scope.ServiceProvider.GetRequiredService<IHackerNewsClient>();
            var cache = scope.ServiceProvider.GetRequiredService<IStoryCache>();

            // Retrieve all available IDs from HackerNews Service
            var currentIds = await client.GetCurrentStoriesAsync(ct);
            if (currentIds.Length == 0) return;

            var currentList = cache.GetOrderedList().Select(s => s.id).ToHashSet();
            var (idsToAdd, idsToDel) = StoryDifferences.GetDelta(currentIds, currentList);

            if (idsToAdd.Count == 0 && idsToDel.Count == 0) return;

            _logger.LogInformation("[memory] Syncing {add} new, {del} old.", idsToAdd.Count, idsToDel.Count);

            // Remove olds IDs from cache list
            if (idsToDel.Count > 0) 
                cache.RemoveOldIds(idsToDel);

            // Add new IDs to cache list - needs to query Hacker news API for details
            if (idsToAdd.Count > 0)
            {
                long start = Stopwatch.GetTimestamp();

                await Parallel.ForEachAsync(idsToAdd,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = _settings.CurrentValue.MaxConcurrentRequests,
                        CancellationToken = ct
                    },
                    async (id, ctoken) =>
                    {
                        var story = await client.GetStoryDetailsAsync(id, ctoken);
                        if (story.id > 0)
                        {
                            if (!cache.TryAdd(story))
                            {
                                _logger.LogWarning("Cache full, skipping story {Id}", id);
                            }
                        }
                    });

                double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                _queryTimeHistorgram.Record(elapsed);
                _logger.LogDebug("-> Timespan to query NEW Story details in Hacker News: {TimeInMs}ms", elapsed);

            }

            // Final ordering of the IDs in cache
            await cache.RebuildOrderedListAsync();
        }
    }
}
