using System.Text;
using IdentityService.Domain.Constants;
using IdentityService.Shared.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProductService.API.Middleware;
using ProductService.Application;
using ProductService.Application.Mappings;
using ProductService.Infrastructure;
using ProductService.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
    options.AddPolicy(AllPermissions.ProductTransfer, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.ProductTransfer)));
});

builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();

// Add HttpClient for inter-service communication
builder.Services.AddHttpClient("RouteService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:RouteService"]!);
});

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Apply migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
    dbContext.Database.Migrate();
    dbContext.Database.EnsureCreated();
}

app.Run();