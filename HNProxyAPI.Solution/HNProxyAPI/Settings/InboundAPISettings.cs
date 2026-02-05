using System.ComponentModel.DataAnnotations;

namespace HNProxyAPI.Settings
{
    public class InboundAPISettings
    {
        [Required]
        [Range(1000, 30000, ErrorMessage = "Timeout for request to this API must be between 1000ms and 30000ms.")]
        public int GlobalRequestTimeoutMs { get; set; } = 30000;

        [Required]
        [Range(1, 10000, ErrorMessage = "Maximum requests per window must be between 1 and 1000.")]
        public int MaxRequestsPerWindow { get; set; } = 100;

        [Required]
        [Range(1, 360, ErrorMessage = "Rate limit window (in secs) range must be between 1 and 360s.")]
        public int RateLimitWindowSeconds { get; set; } = 60;

        [Required]
        [Range(0, 1000, ErrorMessage = "Queue limit range must be between 0 and 1000.")]
        public int QueueLimit { get; set; } = 50;
    }
}
