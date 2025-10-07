using IdentityService.Application.Services;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Data;
using IdentityService.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);


builder.Logging.ClearProviders();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ApplicationName", "Identity Service")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Console()
    .WriteTo.Seq(
        serverUrl: builder.Configuration.GetConnectionString("Seq") ?? "http://seq:80",
        restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();
builder.Host.UseSerilog();

Log.Information("Starting IdentityService");

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Identity Service API",
        Version = "v1",
        Description = "API for managing user authentication and authorization"
    });

    //Add JWT Authentication support
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n " +
                      "Enter your token in the text input below.\r\n\r\n" +
                      "Example: \"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\""
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
});

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("LoginPolicyPerIP", context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        // After UseForwardedHeaders, RemoteIpAddress should have the real client IP
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Handle IPv6-mapped IPv4 addresses (::ffff:192.168.1.1 -> 192.168.1.1)
        if (clientIp.StartsWith("::ffff:"))
        {
            clientIp = clientIp.Substring(7); // Remove the ::ffff: prefix
        }

        // If we're still seeing internal Docker IPs, something is wrong with header forwarding
        if (clientIp.StartsWith("172.") || clientIp.StartsWith("10.") || clientIp == "unknown")
        {
            logger.LogWarning(
                "Rate limiting detected internal IP: {ClientIp} - checking X-Forwarded-For header",
                clientIp);

            // Fallback: manually parse X-Forwarded-For as last resort
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                // Get the first (leftmost) IP in the chain - that's the real client
                var firstIp = forwardedFor.Split(',')[0].Trim();

                // Clean up IPv6 format if present
                if (firstIp.StartsWith("::ffff:"))
                {
                    firstIp = firstIp.Substring(7);
                }

                // Only use this IP if it's NOT an internal Docker IP
                if (!firstIp.StartsWith("172.") && !firstIp.StartsWith("10."))
                {
                    clientIp = firstIp;
                    logger.LogInformation("Using X-Forwarded-For IP: {ClientIp}", clientIp);
                }
                else
                {
                    logger.LogError(
                        "X-Forwarded-For contains only internal IPs: {ForwardedFor} - Nginx may not be setting headers correctly",
                        forwardedFor);
                }
            }
        }

        logger.LogInformation(
            "Rate limiting login - Final Client IP: {ClientIp} | X-Forwarded-For: {ForwardedFor} | RemoteIP: {RemoteIp}",
            clientIp,
            context.Request.Headers["X-Forwarded-For"].ToString(),
            context.Connection.RemoteIpAddress?.ToString());

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    // Configure what happens when rate limit is exceeded
    options.OnRejected = async (context, cancellationToken) =>
    {
        // Log the rate limit rejection
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

        var ipAddress = context.HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim()
            ?? context.HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault()
            ?? context.HttpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";

        logger.LogWarning(
            "Rate limit exceeded for IP: {IpAddress} on endpoint: {Endpoint}",
            ipAddress,
            context.HttpContext.Request.Path);

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";

        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue)
            ? retryAfterValue.TotalSeconds
            : 300; // 5 minutes default

        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Too many login attempts from your IP address. Please try again later.",
            retryAfter = retryAfter,
            // Help the user understand what happened
            message = $"Your IP address has been temporarily blocked due to multiple failed login attempts. Please wait {Math.Ceiling(retryAfter / 60)} minutes before trying again."
        }, cancellationToken: cancellationToken);
    };
});

//Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

// Database
builder.Services.AddDbContext<IdentityDbContext>(options =>
{ 
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
    npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
        npgsqlOptions.CommandTimeout(30);
    })
        .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
        .EnableDetailedErrors(builder.Environment.IsDevelopment());
},
ServiceLifetime.Scoped);


// Identity
builder.Services.AddIdentity<User, Role>(options =>
{
    // Password requirements
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // Sign-in settings
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<IdentityDbContext>()
.AddDefaultTokenProviders();

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!))
    };
});

// Services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,

    // CRITICAL: Tell ASP.NET Core to trust headers from Docker network
    // In Docker, internal IPs are typically 172.x.x.x
    KnownNetworks = { }, // Clear defaults
    KnownProxies = { },  // Clear defaults

    // Allow up to 2 proxies (Nginx ? API Gateway ? Identity Service)
    ForwardLimit = 2,

    // This is the key setting: Trust ALL proxies in Docker network
    // Since containers are isolated, this is safe in your environment
    RequireHeaderSymmetry = false,

    // Process the full X-Forwarded-For chain
    ForwardedForHeaderName = "X-Forwarded-For"
});


app.UseRateLimiter();

app.UseCors("AllowAll");

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Ensure database is created and seeded
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await context.Database.MigrateAsync();
    await  context.Database.EnsureCreatedAsync();
}

app.Run();