using System.Threading.Channels;

namespace HNProxyAPI.Log
{
    /// <summary>
    /// Represents a Channel File Logger engine to write messages in local files
    /// </summary>
    public class ChannelFileLogger : ILogger
    {
        private readonly ChannelWriter<string> _writer;
        private readonly string _categoryName;

        /// <summary>
        /// Iniates a new instance of ChannelFileLogger
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="categoryName"></param>
        public ChannelFileLogger(ChannelWriter<string> writer, string categoryName)
        {
            _writer = writer;
            _categoryName = categoryName;
        }

        /// <summary>
        /// Initates a new scope
        /// </summary>
        /// <typeparam name="TState">State</typeparam>
        /// <param name="state">State</param>
        /// <returns>A disposable object</returns>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <summary>
        /// Adjusts the log level
        /// </summary>
        /// <param name="logLevel">The level to check if enabled.</param>
        /// <returns>If the level is enabled.</returns>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>
        /// Writes a new log message in the producer side
        /// </summary>
        /// <typeparam name="TState">State</typeparam>
        /// <param name="logLevel">Level of the message</param>
        /// <param name="eventId">Id of the event</param>
        /// <param name="state">State</param>
        /// <param name="exception">Exception object</param>
        /// <param name="formatter">Message formatter</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{logLevel}] [{_categoryName}] {formatter(state, exception)}";

            // Non-blocking write to the channel
            _writer.TryWrite(message);
        }
    }

}
