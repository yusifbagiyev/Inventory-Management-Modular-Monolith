using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using InventoryManagement.Web.Extensions;
using InventoryManagement.Web.Middleware;
using InventoryManagement.Web.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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

    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
            // Add custom logic to refresh JWT before cookie expires
            options.Events.OnValidatePrincipal = async context =>
            {
                var jwtToken = context.HttpContext.Session.GetString("JwtToken");
                var refreshToken = context.HttpContext.Session.GetString("RefreshToken");

                // If we don't have tokens, check cookies
                if (string.IsNullOrEmpty(jwtToken))
                {
                    // Try to get from cookies
                    jwtToken = context.Request.Cookies["jwt_token"];
                    if (!string.IsNullOrEmpty(jwtToken))
                    {
                        // Restore to session
                        context.HttpContext.Session.SetString("JwtToken", jwtToken);
                    }
                }

                if (!string.IsNullOrEmpty(jwtToken) && !string.IsNullOrEmpty(refreshToken))
                {
                    // Check if token needs refresh
                    var handler = new JwtSecurityTokenHandler();
                    if (handler.CanReadToken(jwtToken))
                    {
                        var jsonToken = handler.ReadJwtToken(jwtToken);
                        var expiry = jsonToken.ValidTo;

                        // If token expires within 30 minutes, refresh it
                        if (expiry < DateTime.UtcNow.AddMinutes(30))
                        {
                            var authService = context.HttpContext.RequestServices
                                .GetRequiredService<IAuthService>();

                            try
                            {
                                var newTokens = await authService.RefreshTokenAsync(
                                    jwtToken, refreshToken);

                                if (newTokens != null)
                                {
                                    // Update everywhere
                                    context.HttpContext.Session.SetString("JwtToken",
                                        newTokens.AccessToken);
                                    context.HttpContext.Session.SetString("RefreshToken",
                                        newTokens.RefreshToken);

                                    // Update cookies if Remember Me was used
                                    if (context.Request.Cookies.ContainsKey("jwt_token"))
                                    {
                                        var cookieOptions = new CookieOptions
                                        {
                                            HttpOnly = true,
                                            Secure = true,
                                            SameSite = SameSiteMode.Lax,
                                            Expires = DateTimeOffset.Now.AddDays(30)
                                        };

                                        context.Response.Cookies.Append("jwt_token",
                                            newTokens.AccessToken, cookieOptions);
                                        context.Response.Cookies.Append("refresh_token",
                                            newTokens.RefreshToken, cookieOptions);
                                    }
                                }
                            }
                            catch
                            {
                                // If refresh fails, sign out
                                context.RejectPrincipal();
                            }
                        }
                    }
                }
            };
        });


    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromDays(1);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
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

    builder.Services.AddHealthChecks();

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

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();
    app.UseSession();

    app.UseMiddleware<ExceptionHandlerMiddleware>();
    app.UseMiddleware<JwtMiddleware>();

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