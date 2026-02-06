using System.Threading.Channels;

namespace HNProxyAPI.Log
{
    public static class FileLoggingExtensions
    {
        /// <summary>
        /// Connects the new Channel File Logger to the Logging Services
        /// </summary>
        /// <param name="logging">The specified log builder</param>
        /// <param name="filePath">The full director + file / file path</param>
        /// <returns>A new Logging Builder (ILoggingBuilder)</returns>
        public static ILoggingBuilder AddChannelFileLogger(this ILoggingBuilder logging, string filePath)
        {
            var logChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(5000)
            {
                FullMode = BoundedChannelFullMode.DropOldest
            });

            logging.Services.AddSingleton(logChannel.Reader);
            logging.Services.AddSingleton(logChannel.Writer);
            logging.Services.AddHostedService<ChannelLogWriterService>();

            logging.AddProvider(new ChannelFileLoggerProvider(logChannel.Writer));

            return logging;
        }
    }
}
