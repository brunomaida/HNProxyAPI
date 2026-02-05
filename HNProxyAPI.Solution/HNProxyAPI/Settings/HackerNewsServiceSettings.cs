using System.ComponentModel.DataAnnotations;

namespace HNProxyAPI.Settings
{
    public class HackerNewsServiceSettings
    {
        private const string HTTP_CLIENT_NAME = "HackerNewsClient";
        private const string URL_BASE = "https://hacker-news.firebaseio.com/v0/beststories.json";
        private const string URL_BASE_STORY_BY_ID = "https://hacker-news.firebaseio.com/v0/item/{0}.json";

        [Required]
        public string HttpClientName { get; set; } = HTTP_CLIENT_NAME;

        [Required, Url]
        public string UrlBase { get; set; } = URL_BASE;

        [Required, Url]        
        public string UrlBaseStoryById { get; set; } = URL_BASE_STORY_BY_ID;

        [Required]
        [Range(2000, 15000, ErrorMessage = "Request timeout range to Hacker News service must be between 500 and 15000ms.")]
        public int RequestTimeoutMs { get; set; } = 10000;

        [Required]
        [Range(1, 50, ErrorMessage = "Max concurrent requests range must be between 1 and 200.")]
        public int MaxConcurrentRequests { get; set; } = 20;

        [Required]
        [Range(64, 1024, ErrorMessage = "Average object size range must be between 64 and 1024 bytes.")]
        public int AverageObjectSizeBytes { get; set; } = 256; 

        [Required]
        [Range(50 * 1024, 500 * 1024 * 1024, ErrorMessage = "Memory Threshold range must be between 50KB e 500MB.")]
        public long MaxMemoryThresholdBytes { get; set; } = 100 * 1024 * 1024; // 100 MB
    }
}
