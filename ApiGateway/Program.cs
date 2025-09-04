using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();

Log.Logger = new LoggerConfiguration()
.ReadFrom.Configuration(builder.Configuration)
.Enrich.FromLogContext()
.Enrich.WithProperty("ApplicationName", "ProductService")
.Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
.Enrich.WithProperty("MachineName", Environment.MachineName)
.Enrich.WithProperty("ProcessId", Environment.ProcessId)
// Filter out framework noise that causes duplicate-looking logs
.Filter.ByExcluding(logEvent =>
    logEvent.Properties.ContainsKey("SourceContext") &&
    logEvent.Properties["SourceContext"].ToString().Contains("Microsoft.AspNetCore.Hosting.Diagnostics"))
.Filter.ByExcluding(logEvent =>
    logEvent.Properties.ContainsKey("SourceContext") &&
    logEvent.Properties["SourceContext"].ToString().Contains("Microsoft.AspNetCore.Routing"))
    // Only write to Seq to prevent console/file duplication
    .WriteTo.Seq(
        serverUrl: builder.Configuration.GetConnectionString("Seq") ?? "http://localhost:5342",
        restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

builder.Host.UseSerilog();
Log.Information("Starting ApiGateway");

var environment=builder.Environment.EnvironmentName;
//Add Ocelot configuration
builder.Configuration.AddJsonFile("ocelot.json", optional: true, reloadOnChange: true)
                     .AddJsonFile($"ocelot.{environment}.json", optional: true, reloadOnChange: true);

var logger = LoggerFactory.Create(config => config.AddConsole()).CreateLogger("Program");
logger.LogInformation($"Loading Ocelot configuration for environment: {environment}");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebApp",
        policy =>
        {
            var allowedOrigins = builder.Environment.IsDevelopment()
            ? new[] { "http://localhost:5051", "https://localhost:7171" }
            : new[] { "https://inventory166.az", "http://inventory166.az" };

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
builder.Services.AddHealthChecks();

var app = builder.Build();

// Add forwarded headers support for proxy (important when behind reverse proxy like nginx)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseCors("AllowWebApp");

app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        });
        await context.Response.WriteAsync(result);
    }
});

await app.UseOcelot();

app.Run();