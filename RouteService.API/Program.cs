using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using RouteService.Application;
using RouteService.Application.Mappings;
using RouteService.Infrastructure;
using RouteService.Infrastructure.Data;
using Serilog;
using Serilog.Events;
using SharedServices.Authorization;
using SharedServices.HealthChecks;
using SharedServices.Identity;
using SharedServices.RateLimiting;
using SharedServices.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureSanitizedLogging(builder.Configuration);

builder.Logging.ClearProviders();

builder.Services.AddCustomRateLimiting();

Log.Logger = new LoggerConfiguration()
.ReadFrom.Configuration(builder.Configuration)
.Enrich.FromLogContext()
.Enrich.WithProperty("ApplicationName", "RouteService")
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

Log.Information("Starting RouteService API");

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Route Service API",
        Version = "v1",
        Description = "Route Service with JWT Authentication"
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

//Add Cors policy
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

// Add layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

//Add JWT Authentication configuration
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

// Add authorization with permissions
builder.Services.AddAuthorization(options =>
{
    //Add permission policies
    options.AddPolicy(AllPermissions.RouteView, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteView)));
    options.AddPolicy(AllPermissions.RouteCreate, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteCreate)));
    options.AddPolicy(AllPermissions.RouteUpdate, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteUpdate)));
    options.AddPolicy(AllPermissions.RouteDelete, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteDelete)));
    options.AddPolicy(AllPermissions.RouteComplete, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteComplete)));
    options.AddPolicy(AllPermissions.RouteCreateDirect, policy => 
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteCreateDirect)));
    options.AddPolicy(AllPermissions.RouteUpdateDirect, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteUpdateDirect)));
    options.AddPolicy(AllPermissions.RouteDeleteDirect, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteDeleteDirect)));
});

builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RouteDbContext>("database")
    .AddCheck<CustomHealthCheck>("custom");

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            duration = report.TotalDuration.TotalMilliseconds,
            services = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds,
                exception = e.Value.Exception?.Message,
                data = e.Value.Data
            })
        };

        await context.Response.WriteAsJsonAsync(response);
    }
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});

// Apply migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<RouteDbContext>();
    dbContext.Database.Migrate();
    dbContext.Database.EnsureCreated();
}

app.Run();