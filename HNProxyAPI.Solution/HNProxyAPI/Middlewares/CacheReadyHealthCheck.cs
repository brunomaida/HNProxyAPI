using HNProxyAPI.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HNProxyAPI.Middlewares
{
    /// <summary>
    /// Used for Health Check validation after complete system memory load.
    /// </summary>
    public class CacheReadyHealthCheck : IHealthCheck
    {
        private readonly IStoryCache _cache;

        public CacheReadyHealthCheck(IStoryCache cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context">The Health check context</param>
        /// <param name="cancellationToken">A System.Threading.CancellationToken that can be used to cancel the health check.</param>
        /// <returns>A Task that completes when the health check has finished, yielding the status Warming up or Healthy.</returns>
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, 
                                                        CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_cache.IsEmpty
                ? HealthCheckResult.Unhealthy("Cache is warming up...")
                : HealthCheckResult.Healthy("Cache is ready."));
        }
    }
}
