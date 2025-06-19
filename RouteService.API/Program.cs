using System.Text;
using IdentityService.Domain.Constants;
using IdentityService.Shared.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Ocelot.Middleware;
using RouteService.Application;
using RouteService.Application.Mappings;
using RouteService.Infrastructure;
using RouteService.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

// Add layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowProductService", builder =>
    {
        builder.WithOrigins("http://localhost:5090")
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

//Add JWT Authentication configuration
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });


// Add authorization with permissions
builder.Services.AddAuthorization(options =>
{
    //Add permission policies
    options.AddPolicy(AllPermissions.RouteView, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteView)));
    options.AddPolicy(AllPermissions.RouteCreate, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteCreate)));
    options.AddPolicy(AllPermissions.RouteUpdate, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteUpdate)));
    options.AddPolicy(AllPermissions.RouteDelete, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteDelete)));
    options.AddPolicy(AllPermissions.RouteBatchDelete, policy =>
        policy.Requirements.Add(new PermissionRequirement(AllPermissions.RouteBatchDelete)));
});

builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("AllowProductService");

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Apply migrations
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<RouteDbContext>();
    dbContext.Database.Migrate();
    dbContext.Database.EnsureCreated();
}

app.Run();