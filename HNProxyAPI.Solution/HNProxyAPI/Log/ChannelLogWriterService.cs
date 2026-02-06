using System.Threading.Channels;

namespace HNProxyAPI.Log
{
    /// <summary>
    /// Service required to register Channel File Logger
    /// </summary>
    public class ChannelLogWriterService : BackgroundService
    {
        private readonly ChannelReader<string> _reader;
        private readonly string _logDir = "Logs";
        private readonly string _logName= "hnproxyapi_logs";

        /// <summary>
        /// Creates the Reader (consumer) the Channel File Logger
        /// </summary>
        /// <param name="reader">The designated reader for the process</param>
        public ChannelLogWriterService(ChannelReader<string> reader)
        {
            _reader = reader;
            if (!Directory.Exists(_logDir))
            {
                Directory.CreateDirectory(_logDir);
            }
        }

        /// <summary>
        /// Collects the logs from the channel and writes into the full file path
        /// </summary>
        /// <param name="stoppingToken">The given cancellation token</param>
        /// <returns>The task responsible to perform the IO operation</returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Continuous processing until the application shuts down
            await foreach (var message in _reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    var hourlyFileName = $"{_logName}_{DateTime.Now:yyyyMMdd_HH}.txt";
                    var fullPath = Path.Combine(_logDir, hourlyFileName);
                    await File.AppendAllTextAsync(fullPath, message + Environment.NewLine, stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to write to log file: {ex.Message}");
                }
            }
        }
    }
}
