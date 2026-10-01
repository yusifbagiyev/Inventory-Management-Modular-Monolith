using InventoryManagement.Web.Extensions;
using InventoryManagement.Web.HealthChecks;
using InventoryManagement.Web.Localization;
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
    builder.Services.AddUiLocalization();
    var mvcBuilder = builder.Services.AddControllersWithViews().AddDataAnnotationsLocalization();
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

    // Polled by the container healthcheck; the deploy waits on it and rolls back if it never passes.
    builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

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

    // Versioned files (asp-append-version adds ?v=<hash>) never change under that URL: the browser
    // keeps them for a year instead of asking again on every page. The rest (fonts, images
    // referenced from CSS) for a day.
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = context =>
        {
            context.Context.Response.Headers.CacheControl = context.Context.Request.Query.ContainsKey("v")
                ? "public, max-age=31536000, immutable"
                : "public, max-age=86400";
        }
    });

    // One line per request with its duration ("HTTP GET /Products responded 200 in 12.3 ms"), after
    // static files so those are not logged; the container healthcheck stays out of the log.
    app.UseSerilogRequestLogging(options => options.GetLevel = (context, _, exception) =>
        exception != null || context.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
        : context.Request.Path.StartsWithSegments("/health") ? Serilog.Events.LogEventLevel.Verbose
        : Serilog.Events.LogEventLevel.Information);

    app.UseUiLocalization();
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
    app.MapHealthChecks("/health").AllowAnonymous();
    // List thumbnails of uploaded photos (as public as the photos themselves, which nginx serves).
    app.MapGet("/thumbs/{width:int}/images/{**path}", async (int width, string path, InventoryManagement.Web.Services.ImageThumbnails thumbnails, HttpContext http, CancellationToken cancellationToken) =>
    {
        var file = await thumbnails.GetAsync(width, path, cancellationToken);
        if (file == null) return Results.NotFound();
        http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return Results.File(file, "image/jpeg");
    }).AllowAnonymous();
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
