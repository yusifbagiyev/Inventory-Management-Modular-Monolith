using ApiGateway.HealthCheck;
using ApiGateway.Middleware;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using System.Net;
using System.Text;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .Filter.ByExcluding(logEvent =>
    {
        // Your custom filter (can't be done in appsettings.json easily)
        if (logEvent.MessageTemplate.Text.Contains("authenticated for path") ||
            logEvent.MessageTemplate.Text.Contains("No authorization needed") ||
            logEvent.MessageTemplate.Text.Contains("route is authenticated scopes"))
        {
            return true;
        }
        return false;
    })
    .CreateLogger();

builder.Host.UseSerilog();
Log.Information("Starting ApiGateway");

var environmentName = builder.Environment.EnvironmentName;

builder.Services.AddHttpClient("OcelotHttpClient")
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30); // Prevent infinite waits
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        // Connection limits to prevent resource exhaustion
        MaxConnectionsPerServer = 100,
        // Timeout for individual operations
        ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true // Only for development!
    })
    // Add retry policy
    .AddPolicyHandler(GetRetryPolicy())
    // Add circuit breaker
    .AddPolicyHandler(GetCircuitBreakerPolicy());

// Configure health checks for the gateway itself
builder.Services.AddHealthChecks()
    // Check system resources
    .AddProcessAllocatedMemoryHealthCheck(
        maximumMegabytesAllocated: 300,
        name: "gateway-memory",
        tags: new[] { "gateway", "memory" })

    // Check if we can reach downstream services
    .AddCheck<ServicesHealthCheck>(
        "downstream-services",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "services", "critical" });



// Add Health Checks UI for monitoring dashboard
builder.Services.AddHealthChecksUI(setup =>
{
    setup.SetEvaluationTimeInSeconds(15); // How often to check health
    setup.MaximumHistoryEntriesPerEndpoint(100); // History to keep
    setup.SetMinimumSecondsBetweenFailureNotifications(60);

    // Add all microservices to monitor
    setup.AddHealthCheckEndpoint("API Gateway", "/health");
    setup.AddHealthCheckEndpoint("Product Service", "http://product-service/health");
    setup.AddHealthCheckEndpoint("Identity Service", "http://identity-service/health");
    setup.AddHealthCheckEndpoint("Route Service", "http://route-service/health");
    setup.AddHealthCheckEndpoint("Approval Service", "http://approval-service/health");
    setup.AddHealthCheckEndpoint("Notification Service", "http://notification-service/health");
})
.AddInMemoryStorage();


var environment=builder.Environment.EnvironmentName;
//Add Ocelot configuration
builder.Configuration.AddJsonFile("ocelot.json", optional: true, reloadOnChange: true)
                     .AddJsonFile($"ocelot.{environment}.json", optional: true, reloadOnChange: true);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp",
        policy =>
        {
            var allowedOrigins = builder.Environment.IsDevelopment()
            ? new[] { "http://localhost:5051", "https://localhost:7171" }
            : new[] { "https://inventory166.az", "https://inventory166.az" };

            policy.WithOrigins(allowedOrigins) // Add your web app URLs
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials(); // Important for authentication
        });
});

//Add JWT Authentication 
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("Bearer",options =>
    {
        // Allow HTTP in development (set to true in production)
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddOcelot();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,

    // Trust all proxies -we're behind nginx which we control
    KnownNetworks =
    {
        new IPNetwork(IPAddress.Parse("172.18.0.0"), 16),  // Docker bridge network
        new IPNetwork(IPAddress.Parse("172.17.0.0"), 16)   // Default Docker network (fallback)
    },

    // Clear defaults to trust everything in our controlled environment
    ForwardLimit = null,
    RequireHeaderSymmetry = false,
    ForwardedForHeaderName = "X-Forwarded-For"
});

app.UseMiddleware<RequestTimeoutMiddleware>(TimeSpan.FromSeconds(30));

app.UseGlobalExceptionHandler();

app.UseCors("AllowWebApp");

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    // Get the real client IP from forwarded headers (set by nginx)
    var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
    var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
    var remoteIp = context.Connection.RemoteIpAddress?.ToString();

    // Log what we're seeing (for debugging)
    Log.Debug("API Gateway received - X-Forwarded-For: {ForwardedFor}, X-Real-IP: {RealIp}, RemoteIP: {RemoteIp}",
        forwardedFor, realIp, remoteIp);

    // Ensure these headers exist for downstream services
    if (string.IsNullOrEmpty(forwardedFor) && !string.IsNullOrEmpty(remoteIp))
    {
        context.Request.Headers["X-Forwarded-For"] = remoteIp;
    }

    if (string.IsNullOrEmpty(realIp) && !string.IsNullOrEmpty(remoteIp))
    {
        context.Request.Headers["X-Real-IP"] = remoteIp;
    }

    await next();
});

await app.UseOcelot();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError() // Handles 5xx and 408
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests) // Handle 429
        .WaitAndRetryAsync(
            retryCount: 2, // Only retry twice
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                Log.Warning("Retry {RetryAttempt} after {Delay}ms due to {StatusCode}",
                    retryAttempt, timespan.TotalMilliseconds, outcome.Result?.StatusCode);
            });
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable) // 503
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5, // Break after 5 consecutive failures
            durationOfBreak: TimeSpan.FromSeconds(30), // Stay open for 30 seconds
            onBreak: (outcome, duration) =>
            {
                Log.Error("Circuit breaker opened for {Duration}s due to {StatusCode}",
                    duration.TotalSeconds, outcome.Result?.StatusCode);
            },
            onReset: () =>
            {
                Log.Information("Circuit breaker reset - service recovered");
            });
}

// Map health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("gateway"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow
        });
    }
});

// Map the Health Check UI
app.MapHealthChecksUI(options =>
{
    options.UIPath = "/health-dashboard";  // Access the dashboard here
    options.ApiPath = "/health-api";
    options.UseRelativeApiPath = false;
    options.UseRelativeResourcesPath = false;
});


app.Run();