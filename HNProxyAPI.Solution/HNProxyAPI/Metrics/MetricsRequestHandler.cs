using HNProxyAPI.Settings;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Diagnostics.Metrics;

public class MetricsRequestHandler : DelegatingHandler
{
    private const string METER_NAME = "Network.MetricsRequestHandler";

    private readonly SemaphoreSlim _semaphore;
    private readonly IOptionsMonitor<HackerNewsServiceSettings> _apiSettings;
    private readonly Meter _meter;
    private readonly Counter<long> _requestCompletedCounter;
    private readonly UpDownCounter<long> _requestActiveCounter;
    private readonly Histogram<double> _requestDurationHistogram;
    private readonly Histogram<double> _queueWaitHistogram;

    public MetricsRequestHandler(IOptionsMonitor<HackerNewsServiceSettings> apiSettings, 
                                 IMeterFactory meterFactory)
    {
        _apiSettings = apiSettings;
        _semaphore = new SemaphoreSlim(_apiSettings.CurrentValue.MaxConcurrentRequests, _apiSettings.CurrentValue.MaxConcurrentRequests);

        // Define the Meter
        // _meter = new Meter(METER_NAME);
        _meter = meterFactory.Create(METER_NAME);

        // 2. Define Instruments
        // Counter: Cumulative total (Good for rates/throughput)
        _requestCompletedCounter = _meter.CreateCounter<long>(
            "http.client.requests_total",
            "requests",
            description: "Total number of HTTP requests completed.");

        // UpDownCounter: Current state (Good for gauges)
        _requestActiveCounter = _meter.CreateUpDownCounter<long>(
            "http.client.active_requests",
            "requests",
            description: "Number of requests currently in-flight");

        // Histogram: Distribution (Good for percentiles P50, P95, P99)
        _requestDurationHistogram = _meter.CreateHistogram<double>(
            "http.client.avg_duration",
            unit: "ms",
            description: "Avg time taken for the request to complete (network only)");

        _queueWaitHistogram = _meter.CreateHistogram<double>(
            "http.client.avg_queue_duration",
            unit: "ms",
            description: "Avg time spent waiting for a concurrency slot");
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var currentSettings = _apiSettings.CurrentValue;

        // Tag to identify the target host in the metrics
        var tags = new TagList { { "host", request.RequestUri?.Host ?? "unknown" } };

        // Sets the timeout for the request
        using var ctsTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        ctsTimeout.CancelAfter(currentSettings.RequestTimeoutMs);

        // Track active requests (Entering the pipeline)
        _requestActiveCounter.Add(1, tags);

        long startWait = Stopwatch.GetTimestamp();

        try
        {
            // 1. Wait for throttle slot 
            await _semaphore.WaitAsync(ctsTimeout.Token);

            // Record Queue Duration
            TimeSpan queueDuration = Stopwatch.GetElapsedTime(startWait);
            _queueWaitHistogram.Record(queueDuration.TotalMilliseconds, tags);

            long startNetwork = Stopwatch.GetTimestamp();

            try
            {
                // Execute base Request
                var response = await base.SendAsync(request, ctsTimeout.Token);

                // Checks status code from 200 to 500
                tags.Add("status_code", (int)response.StatusCode);

                // Increment Success/Fail counter
                _requestCompletedCounter.Add(1, tags);

                return response;
            }
            finally
            {
                // Record Network Duration
                TimeSpan networkDuration = Stopwatch.GetElapsedTime(startNetwork);
                _requestDurationHistogram.Record(networkDuration.TotalMilliseconds, tags);

                // Release the throttle slot
                _semaphore.Release();
            }
        }
        catch (OperationCanceledException) 
        {
            // Timeout occurred
            tags.Add("error_type", "TimeoutException");
            tags.Add("status_code", 0); // 0 indicates timeout
            _requestCompletedCounter.Add(1, tags);
            throw;
        }
        catch (Exception ex)
        {
            // Record errors that didn't return a response (Timeouts, DNS)
            tags.Add("error_type", ex.GetType().Name);
            tags.Add("status_code", 0); // 0 indicates exception
            _requestCompletedCounter.Add(1, tags);
            throw;
        }
        finally
        {
            // Request finished (Leaving the pipeline)
            _requestActiveCounter.Add(-1, tags);
            _requestDurationHistogram.Record(Stopwatch.GetElapsedTime(startWait).TotalMilliseconds, tags);
        }
    }
}
