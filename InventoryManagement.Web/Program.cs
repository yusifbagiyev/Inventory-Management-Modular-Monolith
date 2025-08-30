using InventoryManagement.Web.Extensions;
using InventoryManagement.Web.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using NotificationService.Application.Services;
using Serilog;
using Serilog.Events;

try
{
    Log.Information("Starting InventoryManagement.Web application");

    var builder = WebApplication.CreateBuilder(args);

    Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Seq(builder.Configuration.GetConnectionString("Seq")!)
    .CreateLogger();

    builder.Logging.ClearProviders();

    builder.Host.UseSerilog((context, services, configuration) => configuration
           .ReadFrom.Configuration(context.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext()
           .Enrich.WithProperty("ApplicationName","InventoryManagement.Web")
           .Enrich.WithProperty("Environment",context.HostingEnvironment.EnvironmentName)
           .WriteTo.Seq(
               serverUrl: context.Configuration.GetConnectionString("Seq") ?? "http://localhost:5342",
               restrictedToMinimumLevel: LogEventLevel.Information));

    Log.Information("Starting InventoryManagement.Web application");

    builder.Services.AddControllersWithViews()
        .AddRazorRuntimeCompilation();

    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(
                builder.Configuration.GetValue<int>("Authentication:CookieExpirationMinutes", 480));
            options.SlidingExpiration = true;
            options.Cookie.Name = ".InventoryManagement.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;

            // Proper security settings based on environment
            if (builder.Environment.IsProduction())
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.Domain = builder.Environment.IsProduction() ? "inventory166.az" : null;
            }
            else
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
            }
        });


    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(
            builder.Configuration.GetValue<int>("Authentication:CookieExpirationMinutes", 480));
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.Name = ".InventoryManagement.Session";

        if (builder.Environment.IsProduction())
        {
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        }
    });


    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                context.Response.StatusCode = 401;
            }
            else
            {
                context.Response.Redirect(context.RedirectUri);
            }
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                context.Response.StatusCode = 403;
            }
            else
            {
                context.Response.Redirect(context.RedirectUri);
            }
            return Task.CompletedTask;
        };
    });


    // Configure CORS properly for production
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Production", policy =>
        {
            policy.WithOrigins(
                    "https://inventory.local",
                    "https://www.inventory.local",
                    "https://api.inventory.local")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });

        options.AddPolicy("Development", policy =>
        {
            policy.WithOrigins(
                    "http://localhost:5051",
                    "https://localhost:7171")
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

    var app = builder.Build();

    if (app.Environment.IsProduction())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts(); // Adds HSTS header for security
        app.UseHttpsRedirection(); // Force HTTPS in production
        app.UseCors("Production");
    }
    else
    {
        app.UseDeveloperExceptionPage();
        app.UseCors("Development");
    }

    app.UseSerilogRequestLogging(options =>
    {
        // Customize the message template
        options.MessageTemplate = "Handled {RequestPath} ({RequestMethod}) in {Elapsed:0.0000} ms with status {StatusCode}";

        // Emit debug-level events instead of the defaults
        options.GetLevel = (httpContext, elapsed, ex) =>
        {
            if (ex != null) return LogEventLevel.Error;
            if (httpContext.Response.StatusCode >= 500) return LogEventLevel.Error;
            if (httpContext.Response.StatusCode >= 400) return LogEventLevel.Warning;
            if (elapsed > 1000) return LogEventLevel.Warning;

            var path = httpContext.Request.Path.Value?.ToLower() ?? "";
            if (path.Contains("_vs/browserlink") ||
                path.Contains("_framework/aspnetcore-browser-refresh") ||
                path.EndsWith(".css") ||
                path.EndsWith(".js") ||
                path.EndsWith(".png") ||
                path.EndsWith(".jpg"))
            {
                return LogEventLevel.Verbose;
            }

            return LogEventLevel.Information;
        };

        // Attach additional properties to the request completion event
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown");
            diagnosticContext.Set("UserId", httpContext.User?.Identity?.Name ?? "Anonymous");
        };
    });


    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();
    app.UseSession();

    app.UseMiddleware<ExceptionHandlerMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHub<NotificationHub>("/notificationHub");

    app.UseMiddleware<JwtMiddleware>();

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