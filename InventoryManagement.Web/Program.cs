using InventoryManagement.Web.Extensions;
using InventoryManagement.Web.Middleware;
using InventoryManagement.Web.Services;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Logging.ClearProviders();

    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .WriteTo.Console()
        .CreateLogger();

    builder.Host.UseSerilog();

    Log.Information("Starting InventoryManagement (modular monolith)");

    // Uploaded images live under the web root so they are served as static files.
    builder.Configuration["ImageSettings:RootPath"] ??= Path.Combine(builder.Environment.WebRootPath, "images");

    // Runtime Razor compilation is a development convenience (it watches the file system and
    // recompiles views on the fly). In production it only costs memory and first-render latency,
    // since the views are already compiled into the assembly at build time.
    var mvcBuilder = builder.Services.AddControllersWithViews();
    if (builder.Environment.IsDevelopment())
        mvcBuilder.AddRazorRuntimeCompilation();

    builder.Services.AddModules(mvcBuilder);
    builder.Services.AddCustomAuthentication(builder.Configuration);
    builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

    builder.Services.AddDistributedMemoryCache();
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
        options.Cookie.MaxAge = TimeSpan.FromDays(7);
        options.IOTimeout = TimeSpan.FromSeconds(30);
    });

    builder.Services.AddHostedService<TokenRefreshBackgroundService>();
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddCustomServices();

    var app = builder.Build();

    app.UseForwardedHeaders();

    if (app.Environment.IsProduction())
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }
    else
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseStaticFiles();
    app.UseRouting();

    // HTML error pages for the UI only; /api keeps its JSON status codes.
    app.UseWhen(context => !ModuleHostExtensions.IsApiRequest(context),
        ui => ui.UseStatusCodePagesWithReExecute("/NotFound", "?statusCode={0}"));

    app.UseSession();
    app.UseMiddleware<ExceptionHandlerMiddleware>();
    app.UseRateLimiter();
    app.UseAuthentication();
    // Keeps the UI session's JWT fresh; /api callers authenticate on their own.
    app.UseWhen(context => !ModuleHostExtensions.IsApiRequest(context), ui => ui.UseMiddleware<JwtMiddleware>());
    app.UseAuthorization();

    app.UseModules();
    app.MapControllers();
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    await app.MigrateModulesAsync();

    Log.Information("InventoryManagement configured successfully");
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
