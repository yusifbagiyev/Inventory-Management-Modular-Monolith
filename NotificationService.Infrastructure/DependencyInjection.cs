using Microsoft.Extensions.DependencyInjection;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Repositories;
using NotificationService.Infrastructure.Data;
using NotificationService.Infrastructure.Repositories;
using NotificationService.Infrastructure.Services;
using SharedServices.LiveUpdates;
using SharedServices.Persistence;

namespace NotificationService.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddModuleDbContext<NotificationDbContext>(NotificationDbContext.Schema);

            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
            services.AddScoped<INotificationInbox, NotificationInbox>();
            services.AddSingleton<ILiveUpdatePublisher, LiveUpdatePublisher>();

            // WhatsApp messages leave one at a time from an outbox, paced for the account's rate limit.
            services.AddSingleton<WhatsAppOutbox>();
            services.AddHostedService<WhatsAppOutboxWorker>();
            services.AddScoped<WhatsAppRouteNotifier>();
            services.AddScoped<SharedServices.Contracts.IWhatsAppRouteNotifier>(sp => sp.GetRequiredService<WhatsAppRouteNotifier>());

            services.AddHttpClient<IWhatsAppService, WhatsAppService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            });

            return services;
        }
    }
}
