using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotificationService.Application.Services;
using NotificationService.Domain.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Infrastructure.Services
{
    public class RabbitMQConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public RabbitMQConsumer(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;

            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:HostName"] ?? "localhost"
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare exchanges and queues
            _channel.ExchangeDeclare("notifications", ExchangeType.Topic, durable: true);
            _channel.QueueDeclare("notification-queue", durable: true, exclusive: false, autoDelete: false);

            // Bind to approval events
            _channel.QueueBind("notification-queue", "notifications", "approval.request.created");
            _channel.QueueBind("notification-queue", "notifications", "approval.request.processed");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (sender, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    using var scope = _serviceProvider.CreateScope();
                    var notificationSender = scope.ServiceProvider.GetRequiredService<NotificationSender>();
                    var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

                    if (ea.RoutingKey == "approval.request.created")
                    {
                        var evt = JsonSerializer.Deserialize<ApprovalRequestCreatedEvent>(message);

                        // Notify all admins
                        await notificationSender.SendToRoleAsync(
                            "Admin",
                            "approval_request",
                            "New Approval Request",
                            $"{evt.RequestedByName} has submitted a {evt.RequestType} request",
                            new { RequestId = evt.RequestId });
                    }
                    else if (ea.RoutingKey == "approval.request.processed")
                    {
                        var evt = JsonSerializer.Deserialize<ApprovalRequestProcessedEvent>(message);

                        // Notify the requester
                        await notificationSender.SendToUserAsync(
                            evt.RequestedById,
                            "approval_processed",
                            $"Request {evt.Status}",
                            evt.Status == "Approved"
                                ? $"Your {evt.RequestType} request has been approved by {evt.ProcessedByName}"
                                : $"Your {evt.RequestType} request has been rejected. Reason: {evt.RejectionReason}",
                            new { RequestId = evt.RequestId });
                    }

                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    // Log error
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume("notification-queue", false, consumer);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        public override void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}