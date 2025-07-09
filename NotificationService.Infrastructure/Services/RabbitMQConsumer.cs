using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Events;
using NotificationService.Domain.Repositories;
using NotificationService.Infrastructure.Data;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Infrastructure.Services
{
    public class RabbitMQConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RabbitMQConsumer> _logger;
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _queueName = "notification-queue";

        public RabbitMQConsumer(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<RabbitMQConsumer> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
                UserName = configuration["RabbitMQ:UserName"] ?? "guest",
                Password = configuration["RabbitMQ:Password"] ?? "guest"
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare exchange and queue
            _channel.ExchangeDeclare("inventory-events", ExchangeType.Topic, durable: true);
            _channel.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false);

            // Bind all event types we want to listen to
            _channel.QueueBind(_queueName, "inventory-events", "approval.request.created");
            _channel.QueueBind(_queueName, "inventory-events", "approval.approved");
            _channel.QueueBind(_queueName, "inventory-events", "approval.rejected");
            _channel.QueueBind(_queueName, "inventory-events", "product.created");
            _channel.QueueBind(_queueName, "inventory-events", "product.deleted");
            _channel.QueueBind(_queueName, "inventory-events", "route.created");
            _channel.QueueBind(_queueName, "inventory-events", "route.completed");
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
                    var routingKey = ea.RoutingKey;

                    _logger.LogInformation($"Received message with routing key: {routingKey}");

                    // Handle different event types based on routing key
                    switch (routingKey)
                    {
                        case "approval.request.created":
                            await HandleApprovalRequestCreated(message);
                            break;
                        case "approval.request.processed":
                            await HandleApprovalRequestProcessed(message);
                            break;
                        case "approval.approved":
                            await HandleApprovalApproved(message);
                            break;
                        case "approval.rejected":
                            await HandleApprovalRejected(message);
                            break;
                        case "product.created":
                            await HandleProductCreated(message);
                            break;
                        case "product.deleted":
                            await HandleProductDeleted(message);
                            break;
                        case "route.created":
                            await HandleRouteCreated(message);
                            break;
                        case "route.completed":
                            await HandleRouteCompleted(message);
                            break;
                    }

                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume(_queueName, false, consumer);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task HandleApprovalRequestCreated(string message)
        {
            try
            {
                var approvalEvent = JsonSerializer.Deserialize<ApprovalRequestCreatedEvent>(message);
                if (approvalEvent == null) return;

                _logger.LogInformation($"Processing approval request from {approvalEvent.RequestedByName}");

                // Get all admin users
                var adminUserIds = await GetUsersInRole("Admin");

                _logger.LogInformation($"Found {adminUserIds.Count} admin users");

                foreach (var adminId in adminUserIds)
                {
                    var notification = new Notification(
                        adminId,
                        "ApprovalRequest",
                        "New Approval Request",
                        $"{approvalEvent.RequestedByName} requested approval for {approvalEvent.RequestType}",
                        JsonSerializer.Serialize(new { approvalRequestId = approvalEvent.RequestId })
                    );

                    await SaveAndSendNotification(notification);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling approval requested event");
            }
        }

        private async Task HandleApprovalRequestProcessed(string message)
        {
            try
            {
                var approvalEvent = JsonSerializer.Deserialize<ApprovalRequestProcessedEvent>(message);
                if (approvalEvent == null) return;

                _logger.LogInformation($"Processing approval response for user {approvalEvent.RequestedById}");

                var title = approvalEvent.Status == "Approved" ? "Request Approved" : "Request Rejected";
                var messageText = approvalEvent.Status == "Approved"
                    ? $"Your {approvalEvent.RequestType} request has been approved by {approvalEvent.ProcessedByName}"
                    : $"Your {approvalEvent.RequestType} request has been rejected. Reason: {approvalEvent.RejectionReason}";

                var notification = new Notification(
                    approvalEvent.RequestedById,
                    "ApprovalResponse",
                    title,
                    messageText,
                    JsonSerializer.Serialize(new { approvalRequestId = approvalEvent.RequestId })
                );

                await SaveAndSendNotification(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling approval processed event");
            }
        }

        private async Task HandleApprovalApproved(string message)
        {
            try
            {
                var approvalEvent = JsonSerializer.Deserialize<ApprovalApprovedEvent>(message);
                if (approvalEvent == null) return;

                _logger.LogInformation($"Processing approval approved for user {approvalEvent.RequestedById}");

                // Notify the requester
                var notification = new Notification(
                    approvalEvent.RequestedById,
                    "ApprovalResponse",
                    "Request Approved",
                    $"Your {approvalEvent.RequestType} request has been approved by {approvalEvent.ApprovedByName}",
                    JsonSerializer.Serialize(new { approvalRequestId = approvalEvent.ApprovalRequestId })
                );

                await SaveAndSendNotification(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling approval approved event");
            }
        }

        private async Task HandleApprovalRejected(string message)
        {
            try
            {
                var approvalEvent = JsonSerializer.Deserialize<ApprovalRejectedEvent>(message);
                if (approvalEvent == null) return;

                var notification = new Notification(
                    approvalEvent.RequestedById,
                    "ApprovalResponse",
                    "Request Rejected",
                    $"Your {approvalEvent.RequestType} request has been rejected. Reason: {approvalEvent.Reason}",
                    JsonSerializer.Serialize(new { approvalRequestId = approvalEvent.ApprovalRequestId })
                );

                await SaveAndSendNotification(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling approval rejected event");
            }
        }

        private async Task HandleProductCreated(string message)
        {
            try
            {
                var productEvent = JsonSerializer.Deserialize<ProductCreatedEvent>(message);
                if (productEvent == null) return;

                _logger.LogInformation($"Product created: {productEvent.Model} ({productEvent.InventoryCode})");

                // Notify all users about new product
                var allUserIds = await GetAllUserIds();

                foreach (var userId in allUserIds)
                {
                    var notification = new Notification(
                        userId,
                        "ProductUpdate",
                        "New Product Added",
                        $"Product {productEvent.Model} ({productEvent.InventoryCode}) has been added to {productEvent.DepartmentName}",
                        JsonSerializer.Serialize(new { productId = productEvent.ProductId })
                    );

                    await SaveAndSendNotification(notification);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling product created event");
            }
        }

        private async Task HandleProductDeleted(string message)
        {
            try
            {
                var productEvent = JsonSerializer.Deserialize<ProductDeletedEvent>(message);
                if (productEvent == null) return;

                _logger.LogInformation($"Product deleted: {productEvent.Model} ({productEvent.InventoryCode})");

                // You can add notification logic here if needed
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling product deleted event");
            }
        }

        private async Task HandleRouteCreated(string message)
        {
            try
            {
                var routeEvent = JsonSerializer.Deserialize<RouteCreatedEvent>(message);
                if (routeEvent == null) return;

                _logger.LogInformation($"Route created for product {routeEvent.Model} to {routeEvent.ToDepartmentName}");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling route created event");
            }
        }

        private async Task HandleRouteCompleted(string message)
        {
            try
            {
                var routeEvent = JsonSerializer.Deserialize<RouteCompletedEvent>(message);
                if (routeEvent == null) return;

                _logger.LogInformation($"Route completed: {routeEvent.RouteId}");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling route completed event");
            }
        }

        private async Task SaveAndSendNotification(Notification notification)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();

            await repository.AddAsync(notification);
            await unitOfWork.SaveChangesAsync();

            _logger.LogInformation($"Sending notification to user {notification.UserId}");

            // Send via SignalR directly using HubContext
            await hubContext.Clients.Group($"user-{notification.UserId}").SendAsync("ReceiveNotification", new
            {
                notification.Id,
                notification.Type,
                notification.Title,
                notification.Message,
                notification.CreatedAt,
                Data = notification.Data != null ? JsonSerializer.Deserialize<object>(notification.Data) : null
            });
        }

        private async Task<List<int>> GetUsersInRole(string role)
        {
            using var scope = _serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

            // Get users for the specific role
            var users = await userService.GetUsersAsync(role);
            return users.Select(u => u.Id).ToList();
        }

        private async Task<List<int>> GetAllUserIds()
        {
            using var scope = _serviceProvider.CreateScope();
            var userService = scope.ServiceProvider.GetRequiredService<IUserService>();

            // Get all users (passing null or empty string for all users)
            var users = await userService.GetUsersAsync(null);
            return users.Select(u => u.Id).ToList();
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}