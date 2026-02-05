namespace HNProxyAPI.Services
{
    public class HackerNewsCacheWarmUpService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HackerNewsCacheWarmUpService> _logger;

        public HackerNewsCacheWarmUpService(IServiceProvider serviceProvider, 
                                            ILogger<HackerNewsCacheWarmUpService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[memory] Warming up with 1st load...");

            using (var scope = _serviceProvider.CreateScope())
            {
                var queryService = scope.ServiceProvider.GetRequiredService<HackerNewsQueryService>();

                try
                {
                    var items = await queryService.GetBestStoriesOrderedByScoreAsync(stoppingToken);
                    _logger.LogInformation("[memory] Cache warm up completed with {items} items", items.Count());                    
                }
                catch (Exception ex)
                {
                    _logger.LogCritical(ex, "[memory] Failed to wamp up cache during service start.");
                }
            }
        }
    }
}
