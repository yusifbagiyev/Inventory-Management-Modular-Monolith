using System.Net.Sockets;
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

            // WhatsApp messages leave one at a time from an outbox, paced for the account's rate limit
            services.AddSingleton<WhatsAppOutbox>();
            services.AddHostedService<WhatsAppOutboxWorker>();
            services.AddScoped<WhatsAppRouteNotifier>();
            services.AddScoped<SharedServices.Contracts.IWhatsAppRouteNotifier>(sp => sp.GetRequiredService<WhatsAppRouteNotifier>());

            // A send that times out is not retried, since it may have been delivered, so the answer gets a long wait
            services.AddHttpClient<IWhatsAppService, WhatsAppService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { ConnectCallback = ConnectWithTimeoutAsync });

            return services;
        }

        private static readonly TimeSpan WhatsAppConnectTimeout = TimeSpan.FromSeconds(10);

        /// <summary>Opens the connection with its own time limit, so a server that cannot be reached fails as a connection error that is safe to retry.</summary>
        private static async ValueTask<Stream> ConnectWithTimeoutAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(WhatsAppConnectTimeout);
                try
                {
                    await socket.ConnectAsync(context.DnsEndPoint, timeout.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new SocketException((int)SocketError.TimedOut);
                }
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    }
}
