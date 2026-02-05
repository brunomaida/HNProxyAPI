using HNProxyAPI.Settings;
using Microsoft.Extensions.Options;

namespace HNProxyAPI.Middlewares
{
    public class GlobalTimeout
    {
        private readonly RequestDelegate _next;

        public GlobalTimeout(RequestDelegate next)
        {
            _next = next;
        }

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
                    await context.Response.WriteAsync("Timeout Global da API excedido.");
                }
            }
        }
    }
}
