using BenchmarkDotNet.Parameters;
using BenchmarkDotNet.Running;
using HNProxyAPI.Benchmarks;
using HNProxyAPI.Tests.Benchmark;
using HNProxyAPI.Tests.Benchmarks;

namespace HNProxyAPI.Tests
{
    public class Program
    {
        public static void main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

            var gtbm = BenchmarkRunner.Run<GlobalTimeoutBenchmark>();
            var hncbm = BenchmarkRunner.Run<HackerNewsClientBenchmark>();
            var serbm = BenchmarkRunner.Run<SerializationBenchmark>();
            var scperfbm = BenchmarkRunner.Run<StoryCachePerformanceBenchmark>();
            var scSyncbm = BenchmarkRunner.Run<StoryCacheSyncBenchmark>();
            var sysbm = BenchmarkRunner.Run<SystemBenchmark>();
        }  
    }
}
