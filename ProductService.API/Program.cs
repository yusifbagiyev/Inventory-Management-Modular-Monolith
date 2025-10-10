using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ProductService.API.Authentication;
using ProductService.API.HealthChecks;
using ProductService.API.Middleware;
using ProductService.Application;
using ProductService.Application.Mappings;
using ProductService.Infrastructure;
using ProductService.Infrastructure.Data;
using Serilog;
using SharedServices.Authorization;
using SharedServices.Identity;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration) // This reads from appsettings.json
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

Log.Information("Starting ProductService API");


// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Product Service API",
        Version = "v1",
        Description = "Product Service with JWT Authentication"
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

// Register custom health check services
builder.Services.AddSingleton<DatabaseHealthCheck>();
builder.Services.AddSingleton<DependencyHealthCheck>();

// Configure comprehensive health checks for production
builder.Services.AddHealthChecks()
    // Custom database check with migration verification
    .AddCheck<DatabaseHealthCheck>(
        "database-advanced",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "db", "critical" })

    // Basic database connectivity check (faster, used for liveness)
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "database-basic",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "db", "basic" },
        timeout: TimeSpan.FromSeconds(2))

    // RabbitMQ check - degraded if unavailable since we can work without it temporarily
    .AddRabbitMQ(
        rabbitConnectionString: $"amqp://{builder.Configuration["RabbitMQ:UserName"]}:{builder.Configuration["RabbitMQ:Password"]}@{builder.Configuration["RabbitMQ:HostName"]}:5672",
        name: "rabbitmq",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "messaging", "rabbitmq" },
        timeout: TimeSpan.FromSeconds(3))

    // Storage check - ensure we can write product images
    .AddCheck("storage", () =>
    {
        var imagePath = builder.Configuration["ImageSettings:Path"]!;
        if (!Directory.Exists(imagePath))
        {
            try
            {
                Directory.CreateDirectory(imagePath);
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy($"Cannot create storage directory: {ex.Message}");
            }
        }

        // Verify write permissions
        var testFile = Path.Combine(imagePath, $".health-{Guid.NewGuid()}");
        try
        {
            File.WriteAllText(testFile, DateTime.UtcNow.ToString());
            File.Delete(testFile);
            return HealthCheckResult.Healthy("Storage is accessible and writable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded($"Storage exists but may have permission issues: {ex.Message}");
        }
    }, tags: new[] { "storage", "io" })

    // Memory check - important for production monitoring
    .AddProcessAllocatedMemoryHealthCheck(
        maximumMegabytesAllocated: 500,
        name: "memory",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "memory", "performance" })

    // Check dependencies
    .AddCheck<DependencyHealthCheck>(
        "dependencies",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "dependencies" });


// Add CORS policy
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

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

//Add JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "JWT_OR_APIKEY";
    options.DefaultChallengeScheme = "JWT_OR_APIKEY";
})
    .AddJwtBearer(options =>
    {
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
    })
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey",null)
    .AddPolicyScheme("JWT_OR_APIKEY", "JWT_OR_APIKEY", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            // Check if the request has an API key header
            if (context.Request.Headers.ContainsKey("X-Api-Key"))
            {
                return "ApiKey";
            }
            // Otherwise, use JWT Bearer authentication
            return "Bearer";
        };
    });

//Add Authorization with permissions
builder.Services.AddAuthorization(options =>
{
    //Add permission policies
    options.AddPolicy(AllPermissions.ProductView, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.ProductView)));
    options.AddPolicy(AllPermissions.ProductCreate, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.ProductCreate)));
    options.AddPolicy(AllPermissions.ProductUpdate, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.ProductUpdate)));
    options.AddPolicy(AllPermissions.ProductDelete, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.ProductDelete)));
    options.AddPolicy(AllPermissions.ProductCreateDirect, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.ProductCreateDirect)));
    options.AddPolicy(AllPermissions.ProductUpdateDirect, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.ProductUpdateDirect)));
    options.AddPolicy(AllPermissions.ProductDeleteDirect, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.ProductDeleteDirect)));
});

builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();


// Apply migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    await dbContext.Database.MigrateAsync();
    await dbContext.Database.EnsureCreatedAsync();
}


// Liveness probe - Is the service running? (used by Docker/K8s to restart unhealthy containers)
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("basic"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            service = "ProductService"
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
});

// Readiness probe - Is the service ready to handle requests?
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("critical"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
}); 

// Detailed health check - Full diagnostic information
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});


app.Run();