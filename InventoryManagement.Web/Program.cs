using InventoryManagement.Web.Extensions;
using InventoryManagement.Web.Middleware;
using Microsoft.AspNetCore.DataProtection;
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

    // Keys protect the auth cookie and antiforgery tokens. Persisted outside the container so a
    // redeploy does not sign everybody out.
    var dataProtection = builder.Services.AddDataProtection().SetApplicationName("InventoryManagement");
    if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
        dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

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

    // HTML error pages for page navigations only; /api and AJAX callers keep their status codes
    // (an AJAX 401 re-executed into an HTML 404 page is useless to the client).
    app.UseWhen(context => !ModuleHostExtensions.IsApiRequest(context)
                           && context.Request.Headers.XRequestedWith != "XMLHttpRequest",
        ui => ui.UseStatusCodePagesWithReExecute("/NotFound", "?statusCode={0}"));

    app.UseMiddleware<ExceptionHandlerMiddleware>();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.UseModules();
    app.MapControllers();
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    await app.Services.MigrateModulesAsync();

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

/// <summary>Entry point type, public so integration tests can host the app.</summary>
public partial class Program;
