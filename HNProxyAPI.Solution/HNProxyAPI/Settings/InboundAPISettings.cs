using System.ComponentModel.DataAnnotations;

namespace HNProxyAPI.Settings
{
    /// <summary>
    /// Stores configuration when handling external requests
    /// </summary>
    public class InboundAPISettings
    {
        /// <summary>
        /// The min/max global request timeout for returning requested data (range: 0 to 300000s)
        /// </summary>
        [Required]
        [Range(0, 30000, ErrorMessage = "Timeout for request to this API must be between 0ms and 30000ms.")]
        public int GlobalRequestTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Maximum number of requests per window (time), defined in RateLimitWindowSeconds (range: 1 to 10000)
        /// </summary>
        [Required]
        [Range(1, 10000, ErrorMessage = "Maximum requests per window must be between 1 and 10000.")]
        public int MaxRequestsPerWindow { get; set; } = 1000;

        /// <summary>
        /// Maximum window size in seconds to measure max quantity of requests (range: 1 to 360)
        /// </summary>
        [Required]
        [Range(1, 360, ErrorMessage = "Rate limit window (in secs) range must be between 1 and 360s.")]
        public int RateLimitWindowSeconds { get; set; } = 60;

        /// <summary>
        /// Queue size for waiting requests when MaxRequestsPerWindow has reached its maximum (range: 0 to 1000)
        /// </summary>
        [Required]
        [Range(0, 1000, ErrorMessage = "Queue limit range must be between 0 and 1000.")]
        public int QueueLimit { get; set; } = 50;
    }
}
