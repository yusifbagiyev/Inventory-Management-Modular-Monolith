using InventoryManagement.Web.Extensions;
using InventoryManagement.Web.Middleware;
using InventoryManagement.Web.Services;
using Serilog;
using Serilog.Events;

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Logging.ClearProviders();

    Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("ApplicationName", "Inventory Web")
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .WriteTo.Console()
    .WriteTo.Seq(
        serverUrl: builder.Configuration.GetConnectionString("Seq") ?? "http://seq:80",
        restrictedToMinimumLevel: LogEventLevel.Information)
    .CreateLogger();

    builder.Host.UseSerilog();

    Log.Information("Starting InventoryManagement.Web application");

    builder.Services.AddControllersWithViews()
        .AddRazorRuntimeCompilation();

    builder.Services.AddCustomAuthentication(builder.Configuration);

    builder.Services.AddDistributedMemoryCache(); // Add this for better session handling
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromHours(8);
        options.Cookie.Name = ".InventoryManagement.Session";
        options.Cookie.HttpOnly = true; // Prevent JavaScript access
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always; // Always require HTTPS in production
        options.Cookie.SameSite = SameSiteMode.Strict; // CSRF protection
        options.IOTimeout = TimeSpan.FromSeconds(30);
    });

    builder.Services.AddHostedService<TokenRefreshBackgroundService>();


    // Configure cookie authentication with security
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.Cookie.Name = ".InventoryManagement.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";

        // Handle AJAX requests properly
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


    builder.Services.AddHttpClient<ApiService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });


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

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();
    app.UseSession();

    app.UseMiddleware<ExceptionHandlerMiddleware>();
    app.UseMiddleware<JwtMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();

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