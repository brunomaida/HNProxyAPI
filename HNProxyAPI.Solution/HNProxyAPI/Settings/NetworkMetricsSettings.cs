using System.ComponentModel.DataAnnotations;

namespace HNAPI.Settings
{
    public class NetworkMetricsSettings
    {
        private const string METRICS_NETORK_NAME = "HackerNewsQueryAPI.Network";
        private const string METRICS_NETWORK_VERSION = "1.0.0";

        [Required]
        public string MetricsNetworkName { get; set; } = METRICS_NETORK_NAME;

        [Required]
        public string MetricsNetworkVersion { get; set; } = METRICS_NETWORK_VERSION;
    }
}
