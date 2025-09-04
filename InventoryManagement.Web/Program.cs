using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using InventoryManagement.Web.Extensions;
using InventoryManagement.Web.Middleware;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using NotificationService.Application.Services;
using Serilog;
using Serilog.Events;

try
{
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

    Log.Information("Starting InventoryManagement.Web application");

    builder.Services.AddControllersWithViews()
        .AddRazorRuntimeCompilation();

    builder.Services.AddAuthentication(options =>
    {
        // Set JWT Bearer as the default scheme for everything
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
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

       // Handle tokens from multiple sources for flexibility
       options.Events = new JwtBearerEvents
       {
           OnMessageReceived = context =>
           {
               // For SignalR connections, get token from query string
               var accessToken = context.Request.Query["access_token"];
               var path = context.HttpContext.Request.Path;

               if (!string.IsNullOrEmpty(accessToken) &&
                   path.StartsWithSegments("/notificationHub"))
               {
                   context.Token = accessToken;
               }

               // Also check for token in session/cookies as fallback
               if (string.IsNullOrEmpty(context.Token))
               {
                   context.Token = context.HttpContext.Session.GetString("JwtToken")
                                ?? context.Request.Cookies["jwt_token"];
               }

               return Task.CompletedTask;
           },

           // Handle authentication challenges (when user is not authenticated)
           OnChallenge = context =>
           {
               context.HandleResponse();

               // For AJAX requests, return 401 instead of redirecting
               if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
               {
                   context.Response.StatusCode = 401;
                   context.Response.ContentType = "application/json";
                   return context.Response.WriteAsync(JsonSerializer.Serialize(new
                   {
                       error = "Unauthorized",
                       message = "Please login to access this resource"
                   }));
               }

               // For regular requests, redirect to login
               var returnUrl = context.Request.Path + context.Request.QueryString;
               context.Response.Redirect($"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}");
               return Task.CompletedTask;
           }
       };
   });



    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromDays(
            builder.Configuration.GetValue<int>("Authentication:CookieExpirationDays", 7));
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
           ? CookieSecurePolicy.SameAsRequest
           : CookieSecurePolicy.Always;
    });

    // Configure CORS properly for production
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowedOrigins", policy =>
        {
            var allowedOrigins = builder.Environment.IsDevelopment()
                ? new[] { "http://localhost:5051", "https://localhost:7171" }
                : new[] { "https://inventory166.az", "http://inventory166.az" };

            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });

    builder.Services.AddHttpClient();
    builder.Services.AddHttpContextAccessor();

    builder.Services.AddCustomServices();

    builder.Services.AddSignalR();

    builder.Services.AddMemoryCache();

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    if (app.Environment.IsProduction())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts(); // Adds HSTS header for security
        app.UseHttpsRedirection(); // Force HTTPS in production
        app.UseCors("AllowedOrigins");
    }
    else
    {
        app.UseDeveloperExceptionPage();
        app.UseCors("AllowedOrigins");
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();
    app.UseSession();

    app.UseMiddleware<ExceptionHandlerMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHub<NotificationHub>("/notificationHub");

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

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    Log.Information("InventoryManagement.Web application configured successfully");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}