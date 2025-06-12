using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProductService.Application.Events;
using ProductService.Application.Interfaces;
using ProductService.Domain.Repositories;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ProductService.Infrastructure.Services
{
    public class RabbitMQConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RabbitMQConsumer> _logger;
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _queueName = "product-transfers";

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
            _channel.QueueBind(_queueName, "inventory-events", "product.image.updated");
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

                    var transferEvent = JsonSerializer.Deserialize<ProductTransferredEvent>(message);
                    if (transferEvent != null)
                        await ProcessProductTransfer(transferEvent);

                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };
            _channel.QueueBind(_queueName, "inventory-events", "product.transferred");
            _channel.BasicConsume(_queueName, false, consumer);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task ProcessProductTransfer(ProductTransferredEvent transferEvent)
        {
            using var scope = _serviceProvider.CreateScope();
            var productRepository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var imageService = scope.ServiceProvider.GetRequiredService<IImageService>();

            var product = await productRepository.GetByIdAsync(transferEvent.ProductId);
            if (product == null)
            {
                _logger.LogWarning($"Product {transferEvent.ProductId} not found");
                return;
            }

            // Update product info
            product.UpdateAfterRouting(transferEvent.ToDepartmentId, transferEvent.ToWorker);

            // Update image if provided
            if(transferEvent.ImageData != null && transferEvent.ImageData.Length > 0)
            {
                // Delete old image
                if (!string.IsNullOrEmpty(product.ImageUrl))
                    await imageService.DeleteImageAsync(product.ImageUrl);

                // Upload new image
                using var stream = new MemoryStream(transferEvent.ImageData);
                var imageUrl = await imageService.UploadImageAsync(
                    stream,
                    transferEvent.ImageFileName ?? $"{product.InventoryCode}.jpg",
                    product.InventoryCode);

                product.UpdateImage(imageUrl);
            }

            await productRepository.UpdateAsync(product);
            await unitOfWork.SaveChangesAsync();
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }
    }
}