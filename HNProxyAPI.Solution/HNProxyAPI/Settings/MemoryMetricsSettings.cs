using System.ComponentModel.DataAnnotations;

namespace HNAPI.Settings
{
    [Obsolete]
    public class MemoryMetricsSettings
    {
        private const string METRICS_MEMORY_NAME = "HackerNewsQueryAPI.Memory";
        private const string METRICS_MEMORY_VERSION = "1.0.0";

        [Required]
        public string MetricsMemoryName { get; set; } = METRICS_MEMORY_NAME;

        [Required]
        public string MetricsMemoryVersion { get; set; } = METRICS_MEMORY_VERSION;
    }
}
