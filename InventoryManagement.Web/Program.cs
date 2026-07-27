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
        .ReadFrom.Configuration(builder.Configuration)
        .WriteTo.Console()
        .CreateLogger();

    builder.Host.UseSerilog();

    Log.Information("Starting InventoryManagement.Web application");

    // Runtime Razor compilation is a development convenience (it watches the file system and
    // recompiles views on the fly). In production it only costs memory and first-render latency,
    // since the views are already compiled into the assembly at build time.
    var mvcBuilder = builder.Services.AddControllersWithViews();
    if (builder.Environment.IsDevelopment())
        mvcBuilder.AddRazorRuntimeCompilation();

    builder.Services.AddCustomAuthentication(builder.Configuration);

    builder.Services.AddDistributedMemoryCache(); // Add this for better session handling
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromDays(7);
        options.Cookie.Name = ".InventoryManagement.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;

        options.Cookie.MaxAge= TimeSpan.FromDays(7);
        options.IOTimeout = TimeSpan.FromSeconds(30);
    });

    builder.Services.AddHostedService<TokenRefreshBackgroundService>();


    // NOTE: ConfigureApplicationCookie used to be called here to set a 1-day expiry and the
    // AJAX 401/403 handlers. It configures the ASP.NET Core *Identity* cookie scheme, which this
    // app never registers, so none of it ever took effect - the real cookie lived on at 60
    // minutes. That configuration now sits with the actual scheme in AddCustomAuthentication().

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

    app.UseStatusCodePagesWithReExecute("/NotFound", "?statusCode={0}");

    app.UseSession();

    app.UseMiddleware<ExceptionHandlerMiddleware>();

    app.UseAuthentication();
      
    app.UseMiddleware<JwtMiddleware>();

    app.UseAuthorization();

    app.MapControllers();

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