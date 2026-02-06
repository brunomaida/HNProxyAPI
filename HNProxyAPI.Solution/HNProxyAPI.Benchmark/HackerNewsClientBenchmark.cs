using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using HNProxyAPI.Data; // Ensure you have your models namespace
using HNProxyAPI.Services;
using HNProxyAPI.Settings;
using Microsoft.Extensions.Options;
using System.Diagnostics.Metrics;
using System.Net;

namespace HNProxyAPI.Benchmark
{
    [ShortRunJob]
    [MemoryDiagnoser] // Tracks allocations (Gen 0/1/2)
    [SimpleJob(RuntimeMoniker.Net80)]
    public class HackerNewsClientBenchmark : IDisposable
    {
        private HackerNewsClient _hnClient;
        private HttpClient _httpClient;

        // Large JSON payload (Story details)
        private const string STORY_JSON = """
        {
            "by": "iambateman",
            "descendants": 422,
            "id": 46829147,
            "kids": [46829381,46835766,46829308,46837159],
            "score": 1769,
            "time": 1769803524,
            "title": "Antirender: remove the glossy shine on architectural renderings",
            "type": "story",
            "url": "https://antirender.com/"
        }
        """;

        // Array JSON payload (Top Stories)
        private const string IDS_JSON = "[46820360,46808251,46810282,46806773,46829147,46805665]";

        [GlobalSetup]
        public void Setup()
        {
            // Create the Fake Network Handler (Zero Latency)
            // We avoid 'Moq' library here because it uses Reflection, which is too slow for micro-benchmarks.
            var fakeHandler = new FakeHttpMessageHandler(IDS_JSON, STORY_JSON);
            _httpClient = new HttpClient(fakeHandler);
            _httpClient.BaseAddress = new Uri("https://hacker-news.firebaseio.com/");

            // Setup Options
            // We use a manual stub implementation for maximum speed
            var optionsMonitor = new FakeOptionsMonitor(new HackerNewsServiceSettings
            {
                UrlBase = "v0/beststories.json",
                UrlBaseStoryById = "v0/item/{0}.json"
            });

            // Setup Null Logger (We don't want to measure console output speed)
            var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<HackerNewsClient>();

            // Setup Meter Factory
            // We use a dummy factory that returns a no-op meter to avoid OS-level counter overhead affecting results
            var meterFactory = new FakeMeterFactory();

            // Instantiate the System Under Test
            _hnClient = new HackerNewsClient(_httpClient, logger, optionsMonitor, meterFactory);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _httpClient.Dispose();
        }

        [Benchmark]
        public async Task<int[]> GetCurrentStories()
        {
            // Benchmarks the array deserialization + metric recording
            return await _hnClient.GetCurrentStoriesAsync(CancellationToken.None);
        }

        [Benchmark]
        public async Task<Story> GetStoryDetails()
        {
            // Benchmarks the object deserialization + metric recording + URL formatting
            return await _hnClient.GetStoryDetailsAsync(46829147, CancellationToken.None);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    #region High-Performance Test Doubles (Stubs)

    // A custom HttpMessageHandler that returns static JSON immediately.
    // This removes the Network Latency variable, allowing us to measure CPU/Memory of the parser.
    public class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _idsJson;
        private readonly string _storyJson;

        public FakeHttpMessageHandler(string idsJson, string storyJson)
        {
            _idsJson = idsJson;
            _storyJson = storyJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = request != null && request.RequestUri!.ToString().Contains("beststories")
                ? _idsJson
                : _storyJson;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            });
        }
    }

    // Lightweight Stub for Options
    public class FakeOptionsMonitor : IOptionsMonitor<HackerNewsServiceSettings>
    {
        public HackerNewsServiceSettings CurrentValue { get; }
        public FakeOptionsMonitor(HackerNewsServiceSettings settings) => CurrentValue = settings;
        public HackerNewsServiceSettings Get(string name) => CurrentValue;
        public IDisposable? OnChange(Action<HackerNewsServiceSettings, string> listener) => null;
    }

    // Lightweight Stub for Meters
    public class FakeMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new Meter(options.Name);
        public void Dispose() { }
    }

    #endregion
}
