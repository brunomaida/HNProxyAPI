using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace HNProxyAPI.Metrics
{
    /// <summary>
    /// System metrics with observable values of heap, stack and threads for the process
    /// </summary>
    public class SystemMetricsCollector : IDisposable
    {
        private readonly Meter _meter;
        private readonly Process _currentProcess;

        private readonly ObservableGauge<long> _workingSetGauge;
        private readonly ObservableGauge<long> _privateBytesGauge;
        private readonly ObservableGauge<long> _gcHeapSizeGauge;
        private readonly ObservableGauge<long> _gcFragmentedBytesGauge;
        private readonly ObservableGauge<int> _threadCountGauge;
        private readonly ObservableGauge<long> _estimatewdStackSizeGauge;

        public SystemMetricsCollector()
        {
            _meter = new Meter("System.Runtime.Resources");

            _currentProcess = Process.GetCurrentProcess();

            _workingSetGauge = _meter.CreateObservableGauge(
                "os.process.memory.working_set",
                () => { _currentProcess.Refresh(); return _currentProcess.WorkingSet64; },
                unit: "bytes",
                description: "The amount of physical memory allocated for the process.");

            _privateBytesGauge = _meter.CreateObservableGauge(
                "os.process.memory.private_bytes",
                () => { _currentProcess.Refresh(); return _currentProcess.PrivateMemorySize64; },
                unit: "bytes",
                description: "The amount of private memory allocated for the process.");

            _gcHeapSizeGauge = _meter.CreateObservableGauge(
                "dotnet.process.gc.heap_size",
                () => GC.GetTotalMemory(forceFullCollection: false),
                unit: "bytes",
                description: "The total size of the managed heap.");

            _gcFragmentedBytesGauge = _meter.CreateObservableGauge(
                "dotnet.process.gc.fragmented_bytes",
                () => GC.GetGCMemoryInfo().FragmentedBytes,
                unit: "bytes",
                description: "The amount of fragmented memory in the managed heap.");

            _threadCountGauge = _meter.CreateObservableGauge(
                "os.process.threads.count",
                () => { _currentProcess.Refresh(); return _currentProcess.Threads.Count; },
                description: "The number of threads in the process.");

            _estimatewdStackSizeGauge = _meter.CreateObservableGauge<long>(
                "os.process.threads.stack_size",
                () => { _currentProcess.Refresh(); return _currentProcess.Threads.Count * 1024 * 1024; }, // 1MB per thread (estimation)
                unit: "bytes",
                description: "Estimated total stack size for all threads in the process.");
        }

        /// <summary>
        /// Releases the objects and its dependencies in the memory
        /// </summary>
        public void Dispose()
        {
            _meter?.Dispose();
            _currentProcess?.Dispose();
        }   
    }
}
