using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ProductService.API.Middleware;
using ProductService.Application;
using ProductService.Application.Mappings;
using ProductService.Infrastructure;
using ProductService.Infrastructure.Data;
using Serilog;
using Serilog.Events;
using SharedServices.Authorization;
using SharedServices.Identity;

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

Log.Information("Starting ProductService API");


// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Product Service API",
        Version = "v1",
        Description = "Product Service with JWT Authentication"
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

// Add CORS policy
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

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

//Add JWT Authentication
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
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

//Add Authorization with permissions
builder.Services.AddAuthorization(options =>
{
    //Add permission policies
    options.AddPolicy(AllPermissions.ProductView, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.ProductView)));
    options.AddPolicy(AllPermissions.ProductCreate, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.ProductCreate)));
    options.AddPolicy(AllPermissions.ProductUpdate, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.ProductUpdate)));
    options.AddPolicy(AllPermissions.ProductDelete, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.ProductDelete)));
    options.AddPolicy(AllPermissions.ProductCreateDirect, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.ProductCreateDirect)));
    options.AddPolicy(AllPermissions.ProductUpdateDirect, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.ProductUpdateDirect)));
    options.AddPolicy(AllPermissions.ProductDeleteDirect, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.ProductDeleteDirect)));
});

builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();


// Apply migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    await dbContext.Database.MigrateAsync();
    await dbContext.Database.EnsureCreatedAsync();
}

app.Run();