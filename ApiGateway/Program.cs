using System.Text;
using ApiGateway.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ApplicationName", "Api Gateway")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Seq(
        serverUrl: builder.Configuration.GetConnectionString("Seq") ?? "http://seq:80",
        restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

builder.Host.UseSerilog();
Log.Information("Starting ApiGateway");

var environmentName = builder.Environment.EnvironmentName;

// Add security services
builder.Services.AddSingleton<IWafRuleEngine, WafRuleEngine>();
builder.Services.AddSingleton<IRequestThrottler, RequestThrottler>();



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

// Add forwarded headers support for proxy
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseCors("AllowWebApp");

app.UseAuthentication();
app.UseAuthorization();

await app.UseOcelot();

app.Run();