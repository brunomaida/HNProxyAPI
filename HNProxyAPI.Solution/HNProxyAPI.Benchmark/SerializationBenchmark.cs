using BenchmarkDotNet.Attributes;
using HNProxyAPI.Data;
using System.Text.Json;

namespace HNProxyAPI.Benchmark
{
    [ShortRunJob]
    [MemoryDiagnoser]
    public class SerializationBenchmark
    {
        private List<Story> _payload;
        private JsonSerializerOptions _options;

        [Params(100, 500, 5000)] // Scenarios: Home, Full Page, Stress
        public int ItemCount;

        [GlobalSetup]
        public void Setup()
        {
            _options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

            // Generates fake payload
            _payload = Enumerable.Range(0, ItemCount).Select(i => new Story
            {
                id = i,
                title = $"Comparison of serialization speed for item {i}",
                postedBy = "benchmark_user",
                score = i * 10,
                time = DateTime.UtcNow,
                uri = "https://benchmark.net/serialization"
            }).ToList();
        }

        [Benchmark(Baseline = true)]
        public string StandardSerialization()
        {
            // What the Controller does by default
            return JsonSerializer.Serialize(_payload, _options);
        }

        /* GOLDEN TIP: 
           In the future, you can implement Source Generators (JsonContext) 
           to avoid Reflection and beat this benchmark.
        */
        // [Benchmark] 
        // public string SourceGenSerialization() { ... }
    }
}
