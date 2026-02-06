using HNProxyAPI.Data;
using HNProxyAPI.Settings;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace HNProxyAPI.Services
{
    /// <summary>
    /// The engine responsible to query Hacker news data from configured URLs
    /// </summary>
    public class HackerNewsClient : IHackerNewsClient
    {
        private const string METER_NAME = "Network.HackerNewsClient";

        private readonly HttpClient _client;
        private readonly ILogger<HackerNewsClient> _logger;
        private readonly IOptionsMonitor<HackerNewsServiceSettings> _settings;
        private readonly Meter _meter;
        private readonly Histogram<double> _queryTimeHistorgram;  // Histogram for tracking average query time of all new Ids
        private static readonly JsonSerializerOptions _jsonOptions;
        
        static HackerNewsClient()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new StoryConverter() }
            };
        }

        public HackerNewsClient(HttpClient httpClient, 
                                ILogger<HackerNewsClient> logger, 
                                IOptionsMonitor<HackerNewsServiceSettings> serviceSettings,
                                IMeterFactory meterFactory)
        {
            _client = httpClient;
            _logger = logger;
            _settings = serviceSettings;
            _meter = meterFactory.Create(METER_NAME);
            _queryTimeHistorgram = _meter.CreateHistogram<double>(
                "hn.client.query.request_time",
                unit: "ms",
                description: "Avg time to query details a specific Story (id) in Hacker news"
                );
        }        

        /// <summary>
        /// Retrieves current Stories
        /// </summary>
        /// <param name="ct">Cancellation token used in the entire process</param>
        /// <returns></returns>
        public async Task<int[]> GetCurrentStoriesAsync(CancellationToken ct)
        {
            // Base URL for the external web API
            // "https://hacker-news.firebaseio.com/v0/beststories.json";

            // Sample return from HackerNews web API (JSON)
            // [46820360,46808251,46810282,46806773,46829147,46805665,46821774,46812933,46820783,46814743,46809708,46810401,
            // 46821134,46822632,46812173,46814614,46804828,46820924,46817813,46810027,46827003,46824098,46809069,46814089,
            // 46825319,46824003,46819793,46818258,46817200,46816497,46816189,46814147,46823274,46820941,46818731,46816966,
            // 46814621,46809785,46819083,46814117,46813381,46812833,46808769,46805292]

            long start = Stopwatch.GetTimestamp();

            try
            {
                var ids = await _client.GetFromJsonAsync<int[]>(_settings.CurrentValue.UrlBase, ct);
                double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                return ids ?? Array.Empty<int>();
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "Error retrieving current Stories from Hacker News. (timeout/cancelled)");
                return Array.Empty<int>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current Stories from Hacker News.");
                return Array.Empty<int>();
            }
            finally
            {
                double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                _queryTimeHistorgram.Record(elapsed,
                    new KeyValuePair<string, object?>("http.method", "GET"),
                    new KeyValuePair<string, object?>("http.status_code", "200"));

                _logger.LogDebug("-> Timespan to query list of Story IDs in Hacker News: {TimeInMs}ms", elapsed);
            }
        }

        /// <summary>
        /// Retrieves Story details
        /// </summary>
        /// <param name="id">ID of the Story</param>
        /// <param name="ct">Cancellation token used in the entire process</param>
        /// <returns></returns>
        public async Task<Story> GetStoryDetailsAsync(int id, CancellationToken ct)
        {
            /* Sample Story Details (JSON):
             * https://hacker-news.firebaseio.com/v0/item/46829147.json
             * Total http response size in bytes:  
             { 
                "by":"iambateman",
                "descendants":422,
                "id":46829147,
                "kids":[46829381,46835766,46829308,46837159,46831343,46835020,46830624,46830542,46829399,46830571,46829439,46829516,46839210,46837916,46829694,46837610,46834320,46831448,46833817,46829348,46839739,46835010,46838155,46830849,46829522,46829679,46837264,46840051,46834362,46831268,46829549,46831892,46832459,46831828,46835887,46836300,46830437,46831845,46829683,46830259,46830886,46830327,46829443,46833714,46835248,46835963,46833681,46831266,46835079,46836968,46834542,46829980,46831346,46831807,46830096,46831016,46832253,46834244,46835662,46834201,46835312,46835293,46830528,46837010,46831900,46829465,46833467,46833245,46830243,46830301,46837918,46829417,46832123,46830566,46833087,46832037,46835425,46832500,46829538,46829344,46829619,46829907,46834266,46831849,46831797,46831767,46831910,46832349,46831402,46831382,46835089,46830223,46830166,46829525,46829638,46830843,46830990,46837146,46829566,46837361,46833940,46832166,46836729,46829892],
                "score":1769,
                "time":1769803524,
                "title":"Antirender: remove the glossy shine on architectural renderings",  
                "type":"story",
                "url":"https://antirender.com/"},
              */

            long start = Stopwatch.GetTimestamp();

            try
            {
                string url = string.Format(_settings.CurrentValue.UrlBaseStoryById, id);               
                var newStory = await _client.GetFromJsonAsync<Story>(url, ct);
                double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;                
                return newStory;
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "Error retrieving current Story ID{id} from Hacker News. (timeout/cancelled)", id);
                return default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Story(id:{id}) details from Hacker News", id);
                return default;
            }
            finally
            {
                double elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                _queryTimeHistorgram.Record(elapsed,
                    new KeyValuePair<string, object?>("http.method", "GET"),
                    new KeyValuePair<string, object?>("http.status_code", "200"));

                _logger.LogDebug("-> Timespan to query Story{id} Details in Hacker News: {TimeInMs}ms", id, elapsed);
            }
        }
        
    }
}
