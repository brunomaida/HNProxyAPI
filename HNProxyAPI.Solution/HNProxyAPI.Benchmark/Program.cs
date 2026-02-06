using BenchmarkDotNet.Running;
using HNProxyAPI.Benchmark;

var gtbm = BenchmarkRunner.Run<GlobalTimeoutBenchmark>();
var hncbm = BenchmarkRunner.Run<HackerNewsClientBenchmark>();
var serbm = BenchmarkRunner.Run<SerializationBenchmark>();
var scperfbm = BenchmarkRunner.Run<StoryCachePerformanceBenchmark>();
var scSyncbm = BenchmarkRunner.Run<StoryCacheSyncBenchmark>();
var sysbm = BenchmarkRunner.Run<SystemBenchmark>();

Console.ReadKey(); 
