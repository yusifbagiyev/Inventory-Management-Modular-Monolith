using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RouteService.Application.Behaviors;
using RouteService.Application.Interfaces;
using RouteService.Application.Services;
using System.Reflection;

namespace RouteService.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            var assembly = Assembly.GetExecutingAssembly();

            services.AddAutoMapper(assembly);
            services.AddValidatorsFromAssembly(assembly);
            services.AddMediatR(config => config.RegisterServicesFromAssembly(assembly));

            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));


            services.AddScoped<IImageService, ImageService>();
            services.AddScoped<IRouteManagementService, RouteManagementService>();
            return services;
        }
    }
}
