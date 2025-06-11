using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RouteService.Application.DTOs.Commands;
using RouteService.Application.Events;
using RouteService.Application.Features.Routes.Commands;

namespace RouteService.Infrastructure.Services
{
    public class RabbitMQConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RabbitMQConsumer> _logger;
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _queueName = "route-product-created";

        public RabbitMQConsumer(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<RabbitMQConsumer> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMQ:HostName"] ?? "localhost",
                UserName = configuration["RabbitMQ:UserName"] ?? "guest",
                Password = configuration["RabbitMQ:Password"] ?? "guest",
                Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672")
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.ExchangeDeclare("inventory-events", ExchangeType.Topic, durable: true);
            _channel.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false);
            _channel.QueueBind(_queueName, "inventory-events", "product.created");
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
                    var productCreatedEvent = JsonSerializer.Deserialize<ProductCreatedEvent>(message);

                    if (productCreatedEvent != null)
                    {
                        await ProcessProductCreated(productCreatedEvent);
                    }

                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (FluentValidation.ValidationException ex)
                {
                    _logger.LogError(ex, "Validation error - message will be discarded");
                    // Don't requeue validation errors
                    _channel.BasicNack(ea.DeliveryTag, false, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing product created event");
                    // Requeue only for unexpected errors
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _channel.BasicConsume(_queueName, false, consumer);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task ProcessProductCreated(ProductCreatedEvent productCreatedEvent)
        {
            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();


            var dto = new AddNewInventoryDto
            {
                ProductId = productCreatedEvent.ProductId,
                ToDepartmentId = productCreatedEvent.DepartmentId,
                ToWorker = productCreatedEvent.Worker ?? string.Empty,
                IsNewItem = true,
                Notes = $"Auto-created from new product: {productCreatedEvent.Model}"
            };

            await mediator.Send(new AddNewInventory.Command(dto));
            _logger.LogInformation($"Created inventory route for new product {productCreatedEvent.ProductId}");
        }

        public override void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}
