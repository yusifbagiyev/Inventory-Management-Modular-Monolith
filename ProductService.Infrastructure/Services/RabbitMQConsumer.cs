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
        private readonly string _queueName = "product-image-updates";

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
                    var imageEvent = JsonSerializer.Deserialize<ProductImageUpdatedEvent>(message);

                    if (imageEvent != null)
                    {
                        await ProcessImageUpdate(imageEvent);
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

        private async Task ProcessImageUpdate(ProductImageUpdatedEvent imageEvent)
        {
            using var scope = _serviceProvider.CreateScope();
            var productRepository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var imageService = scope.ServiceProvider.GetRequiredService<IImageService>();

            var product = await productRepository.GetByIdAsync(imageEvent.ProductId);
            if (product == null)
            {
                _logger.LogWarning($"Product {imageEvent.ProductId} not found");
                return;
            }

            // Delete old image
            if (!string.IsNullOrEmpty(product.ImageUrl))
            {
                await imageService.DeleteImageAsync(product.ImageUrl);
            }

            // Upload new image
            if (imageEvent.ImageData != null && imageEvent.ImageData.Length > 0)
            {
                using var stream = new MemoryStream(imageEvent.ImageData);
                var imageUrl = await imageService.UploadImageAsync(
                    stream,
                    imageEvent.ImageFileName ?? $"{product.InventoryCode}-{Guid.NewGuid().ToString()}.jpg",
                    product.InventoryCode);

                product.UpdateImage(imageUrl);
                await productRepository.UpdateAsync(product);
                await unitOfWork.SaveChangesAsync();
            }
        }
    }
}
