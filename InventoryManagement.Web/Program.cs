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

    // A save can carry ten photos of up to 5 MB each, and nginx lets 55 MB through, more than Kestrel's 28.6 MB default
    builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 60L * 1024 * 1024);

    // Uploaded images live under the web root so they are served as static files
    builder.Configuration["ImageSettings:RootPath"] ??= Path.Combine(builder.Environment.WebRootPath, "images");

    builder.Services.AddUiLocalization();
    var mvcBuilder = builder.Services.AddControllersWithViews().AddDataAnnotationsLocalization();
    // Views are precompiled, so runtime compilation is only worth it while developing
    if (builder.Environment.IsDevelopment())
        mvcBuilder.AddRazorRuntimeCompilation();

    builder.Services.AddModules(mvcBuilder);
    builder.Services.AddCustomAuthentication(builder.Configuration);
    builder.Services.AddAntiforgery(options =>
    {
        options.HeaderName = "RequestVerificationToken";
        // The framework default never marks this cookie Secure
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

    // Keys live outside the container so a redeploy does not sign everybody out
    var dataProtection = builder.Services.AddDataProtection().SetApplicationName("InventoryManagement");
    if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
        dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddCustomServices();

    // The deploy script waits on this and rolls back if it never passes
    builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");

    var app = builder.Build();

    app.UseForwardedHeaders();

    // Any environment other than Development gets the plain error page, even a mistyped one
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    // A ?v= file never changes under its URL, so it is cached for a year and everything else for a day
    app.UseStaticFiles(new StaticFileOptions
    {
        OnPrepareResponse = context =>
        {
            context.Context.Response.Headers.CacheControl = context.Context.Request.Query.ContainsKey("v")
                ? "public, max-age=31536000, immutable"
                : "public, max-age=86400";
        }
    });

    // After static files so they are not logged, with health checks at Verbose to keep them out of the way
    app.UseSerilogRequestLogging(options => options.GetLevel = (context, _, exception) =>
        exception != null || context.Response.StatusCode >= 500 ? Serilog.Events.LogEventLevel.Error
        : context.Request.Path.StartsWithSegments("/health") ? Serilog.Events.LogEventLevel.Verbose
        : Serilog.Events.LogEventLevel.Information);

    app.UseUiLocalization();

    // Error pages are for page navigations only, and before routing so a 404 never becomes a login redirect
    app.UseWhen(context => !ModuleHostExtensions.IsApiRequest(context)
                           && context.Request.Headers.XRequestedWith != "XMLHttpRequest",
        ui => ui.UseStatusCodePagesWithReExecute("/NotFound", "?statusCode={0}"));

    app.UseRouting();

    app.UseMiddleware<ExceptionHandlerMiddleware>();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.UseModules();
    app.MapControllers();
    app.MapHealthChecks("/health").AllowAnonymous();
    // Anonymous on purpose, since the original photos are public through nginx anyway
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
