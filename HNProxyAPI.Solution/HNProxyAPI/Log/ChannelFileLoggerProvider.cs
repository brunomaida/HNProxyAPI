using System.Threading.Channels;

namespace HNProxyAPI.Log
{
    /// <summary>
    /// Allows the creation of a Channel File Logger
    /// </summary>
    public class ChannelFileLoggerProvider : ILoggerProvider
    {
        private readonly ChannelWriter<string> _writer;

        /// <summary>
        /// Creates a new instance of a Channel File Logger
        /// </summary>
        /// <param name="writer">The writer channel used to output the messages</param>
        public ChannelFileLoggerProvider(ChannelWriter<string> writer) => _writer = writer;

        /// <summary>
        /// Creates a new Channel File Logger with the given name
        /// </summary>
        /// <param name="categoryName">Logger Name</param>
        /// <returns>An ILogger object</returns>
        public ILogger CreateLogger(string categoryName) => new ChannelFileLogger(_writer, categoryName);

        /// <summary>
        /// Disposes internal resources used
        /// </summary>
        public void Dispose() { }
    }
}
