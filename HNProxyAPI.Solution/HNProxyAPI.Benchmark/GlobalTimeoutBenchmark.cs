using BenchmarkDotNet.Attributes;
using HNProxyAPI.Middlewares;
using HNProxyAPI.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using BenchmarkDotNet.TestAdapter;
using Moq;

namespace HNProxyAPI.Benchmark
{
    [MemoryDiagnoser]
    public class GlobalTimeoutBenchmark
    {
        private DefaultHttpContext _context;
        private RequestDelegate _next;
        private GlobalTimeout _middleware;
        private Mock<IOptionsMonitor<InboundAPISettings>> _settings;

        [GlobalSetup]
        public void Setup()
        {
            _context = new DefaultHttpContext();
            _next = (ctx) => Task.CompletedTask; // The rest of the API is instant

            // Setup settings
            _settings = new Mock<IOptionsMonitor<InboundAPISettings>>();
            _settings.Setup(s => s.CurrentValue).Returns(new InboundAPISettings
            {
                GlobalRequestTimeoutMs = 5000
            });

            _middleware = new GlobalTimeout(_next);
        }

        [Benchmark]
        public async Task GlobalTimeout_Overhead()
        {
            // Measures the cost of creating linked CancellationTokenSources and try/catch blocks
            await _middleware.InvokeAsync(_context, _settings.Object);
        }
    }
}