using HNProxyAPI.Settings;
using Microsoft.Extensions.Options;

namespace HNProxyAPI.Middlewares
{
    /// <summary>
    /// Links the Global system timeout/cancellation token to the http execution context
    /// </summary>
    public class GlobalTimeout
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// Creates a new instance of the Global timeout object to control timeouts/cancelations via token
        /// </summary>
        /// <param name="next">A function that can process an HTTP request.</param>
        public GlobalTimeout(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// Executes an async
        /// </summary>
        /// <param name="context">The HttpContext that executes web requests.</param>
        /// <param name="settingsMonitor">The Inbound API Settings containing the Global timeout.</param>
        /// <returns></returns>
        public async Task InvokeAsync(HttpContext context, 
                                      IOptionsMonitor<InboundAPISettings> settingsMonitor)
        {
            var settings = settingsMonitor.CurrentValue;
            if (settings.GlobalRequestTimeoutMs <= 0)
            {
                await _next(context);
                return;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(settings.GlobalRequestTimeoutMs));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, cts.Token);
            context.RequestAborted = linkedCts.Token;

            try
            {
                await _next(context);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
                    await context.Response.WriteAsync("API Global Timeout Global exceeded.");
                }
            }
        }
    }
}
