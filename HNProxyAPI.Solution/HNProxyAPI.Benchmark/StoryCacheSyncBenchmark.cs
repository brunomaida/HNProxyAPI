using BenchmarkDotNet.Attributes;

namespace HNProxyAPI.Benchmark
{
    [ShortRunJob]
    [MemoryDiagnoser]
    public class StoryCacheSyncBenchmark
    {
        private int[] _apiIds;     // IDs arriving from the API (e.g., 500 items)
        private int[] _cachedIds;  // IDs already in memory (e.g., 500 items)

        [Params(500, 10_000)] // Real Scenario vs Future Scenario
        public int Count;

        [GlobalSetup]
        public void Setup()
        {
            // Simulates 20% churn between cache and API
            var rnd = new Random(42);

            _cachedIds = Enumerable.Range(0, Count).ToArray();

            // API brings some new ones and removes some old ones
            _apiIds = Enumerable.Range(0, Count)
                .Select(i => i + (int)(Count * 0.2)) // Shifts 20%
                .ToArray();
        }

        [Benchmark(Baseline = true)]
        public void Naive_List_Approach()
        {
            // WRONG WAY (Slow): Using LINQ on Arrays directly
            // This performs repeated linear scans
            var toAdd = _apiIds.Where(id => !_cachedIds.Contains(id)).ToList();
            var toRemove = _cachedIds.Where(id => !_apiIds.Contains(id)).ToList();
        }

        [Benchmark]
        public void Optimized_HashSet_Approach()
        {
            // CORRECT WAY (Fast): Using HashSet for O(1) lookup
            var cachedSet = new HashSet<int>(_cachedIds);
            var apiSet = new HashSet<int>(_apiIds);

            // Identify new items (API has, Cache doesn't)
            var toAdd = new List<int>();
            foreach (var id in _apiIds)
            {
                if (!cachedSet.Contains(id)) toAdd.Add(id);
            }

            // Identify removed items (Cache has, API doesn't)
            var toRemove = new List<int>();
            foreach (var id in _cachedIds)
            {
                if (!apiSet.Contains(id)) toRemove.Add(id);
            }
        }
    }
}
