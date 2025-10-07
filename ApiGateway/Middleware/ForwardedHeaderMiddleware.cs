namespace ApiGateway.Middleware
{
    public class ForwardedHeaderMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ForwardedHeaderMiddleware> _logger;

        public ForwardedHeaderMiddleware(RequestDelegate next, ILogger<ForwardedHeaderMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Get the original X-Forwarded-For from Nginx (if present)
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
            var realIp = context.Request.Headers["X-Real-IP"].ToString();
            var remoteIp = context.Connection.RemoteIpAddress?.ToString();

            // Log what we received from Nginx
            _logger.LogDebug(
                "API Gateway received headers - X-Forwarded-For: '{ForwardedFor}', X-Real-IP: '{RealIp}', RemoteIP: '{RemoteIp}'",
                forwardedFor, realIp, remoteIp);

            // If we have X-Forwarded-For from Nginx, preserve it
            // Otherwise, start a new chain with the remote IP
            if (string.IsNullOrEmpty(forwardedFor) && !string.IsNullOrEmpty(remoteIp))
            {
                // Nginx didn't set X-Forwarded-For, so we start the chain
                context.Request.Headers["X-Forwarded-For"] = remoteIp;
                _logger.LogInformation("Started X-Forwarded-For chain with: {RemoteIp}", remoteIp);
            }
            else if (!string.IsNullOrEmpty(forwardedFor))
            {
                // Nginx already set X-Forwarded-For, just pass it through
                // Ocelot will automatically forward this header
                _logger.LogInformation("Forwarding existing X-Forwarded-For chain: {ForwardedFor}", forwardedFor);
            }

            // Ensure X-Real-IP is set (use first IP from X-Forwarded-For chain)
            if (string.IsNullOrEmpty(realIp) && !string.IsNullOrEmpty(forwardedFor))
            {
                var firstIp = forwardedFor.Split(',')[0].Trim();
                context.Request.Headers["X-Real-IP"] = firstIp;
                _logger.LogInformation("Set X-Real-IP to: {FirstIp}", firstIp);
            }
            else if (string.IsNullOrEmpty(realIp) && !string.IsNullOrEmpty(remoteIp))
            {
                context.Request.Headers["X-Real-IP"] = remoteIp;
            }

            await _next(context);
        }
    }
}