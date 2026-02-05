using HNProxyAPI.Data;
using HNProxyAPI.Metrics;
using HNProxyAPI.Middlewares;
using HNProxyAPI.Services;
using HNProxyAPI.Settings;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Threading.RateLimiting;

namespace HNProxyAPI.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        // Groups all file configurations required to calibrate the system execution
        public static IServiceCollection AddAppConfiguration(this IServiceCollection services)
        {
            services.AddOptions<InboundAPISettings>()
                .BindConfiguration("InboundAPISettings")
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<HackerNewsServiceSettings>()
                .BindConfiguration("HackerNewsAPISettings")
                .ValidateDataAnnotations()
                .ValidateOnStart();

            return services;
        }

        // Groups the main structure: HackerNews, Metrics, Http
        public static IServiceCollection AddHackerNewsInfrastructure(this IServiceCollection services)
        {
            services.AddMetrics();
            services.AddSingleton<SystemMetricsCollector>();
            services.AddSingleton<IStoryCache, StoryCache>();
            services.AddSingleton<HackerNewsQueryService>();
            //services.AddTransient<MetricsRequestHandler>();

            services.AddHealthChecks().AddCheck<CacheReadyHealthCheck>("CacheReady");

            services.AddHttpClient<IHackerNewsClient, HackerNewsClient>(
                (sp, client) =>
                {
                    var settings = sp.GetRequiredService<IOptions<HackerNewsServiceSettings>>().Value;
                    client.BaseAddress = new Uri(settings.UrlBase);
                    client.Timeout = Timeout.InfiniteTimeSpan;
                })
            //.AddHttpMessageHandler<MetricsRequestHandler>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(2));

            services.AddHostedService<HackerNewsCacheWarmUpService>();

            // Ready to be implemented if required in the future
            //services.AddHostedService<HackerNewsSyncWorker>();

            return services;
        }

        /// <summary>
        /// Isolates the logic for Rate Limiter
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddCustomRateLimiting(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                var sp = services.BuildServiceProvider();
                var settings = sp.GetRequiredService<IOptions<InboundAPISettings>>().Value;
                
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddFixedWindowLimiter("GlobalPolicy", opt =>
                {
                    opt.PermitLimit = settings.MaxRequestsPerWindow;
                    opt.Window = TimeSpan.FromSeconds(settings.RateLimitWindowSeconds);
                    opt.QueueLimit = settings.QueueLimit;
                    opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                });
            });

            return services;
        }
    }
}