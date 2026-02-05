using HNProxyAPI.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HNProxyAPI.Middlewares
{
    public class CacheReadyHealthCheck : IHealthCheck
    {
        private readonly IStoryCache _cache;

        public CacheReadyHealthCheck(IStoryCache cache)
        {
            _cache = cache;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, 
                                                        CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_cache.IsEmpty
                ? HealthCheckResult.Unhealthy("Cache is warming up...")
                : HealthCheckResult.Healthy("Cache is ready."));
        }
    }
}
