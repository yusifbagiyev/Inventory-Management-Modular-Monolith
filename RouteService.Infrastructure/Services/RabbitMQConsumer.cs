using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RouteService.Application.Events;
using RouteService.Application.Interfaces;
using RouteService.Domain.Entities;
using RouteService.Domain.Repositories;
using RouteService.Domain.ValueObjects;

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

                    if (ea.RoutingKey == "product.created")
                    {
                        var createdEvent = JsonSerializer.Deserialize<ProductCreatedEvent>(message);
                        if (createdEvent != null)
                            await ProcessProductCreated(createdEvent);
                    }
                    else if (ea.RoutingKey == "product.deleted")
                    {
                        var deletedEvent = JsonSerializer.Deserialize<ProductDeletedEvent>(message);
                        if (deletedEvent != null)
                            await ProcessProductDeleted(deletedEvent);
                    }
                    else if(ea.RoutingKey == "product.updated")
                    {
                        var updatedEvent = JsonSerializer.Deserialize<ProductUpdatedEvent>(message);
                        if (updatedEvent != null)
                            await ProcessProductUpdated(updatedEvent);
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

            _channel.QueueBind(_queueName, "inventory-events", "product.created");
            _channel.QueueBind(_queueName, "inventory-events", "product.deleted");
            _channel.QueueBind(_queueName, "inventory-events", "product.updated");

            _channel.BasicConsume(_queueName, false, consumer);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task ProcessProductCreated(ProductCreatedEvent productCreatedEvent)
        {
            using var scope = _serviceProvider.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var imageService = scope.ServiceProvider.GetRequiredService<IImageService>();
            var repository = scope.ServiceProvider.GetRequiredService<IInventoryRouteRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            string? imageUrl = null;

            // Upload image if data is provided
            if(productCreatedEvent.ImageData!=null&& productCreatedEvent.ImageData.Length > 0)
            {
                using var stream= new MemoryStream(productCreatedEvent.ImageData);
                imageUrl=await imageService.UploadImageAsync(
                    stream,
                    productCreatedEvent.ImageFileName ?? $"image-{DateTime.Now}.jpg", 
                    productCreatedEvent.InventoryCode);
            }
            var productSnapshot = new ProductSnapshot(
                productCreatedEvent.ProductId,
                productCreatedEvent.InventoryCode,
                productCreatedEvent.Model,
                productCreatedEvent.Vendor,
                productCreatedEvent.CategoryName,
                productCreatedEvent.IsWorking);

            var route = InventoryRoute.CreateNewInventory(
                productSnapshot,
                productCreatedEvent.DepartmentId,
                productCreatedEvent.DepartmentName,
                productCreatedEvent.Worker,
                productCreatedEvent.IsNewItem,
                imageUrl,
                $"Auto-created from product service");
            
            await repository.AddAsync(route);
            route.Complete();
            await unitOfWork.SaveChangesAsync();

            _logger.LogInformation($"Created inventory route for new product {productCreatedEvent.ProductId}");
        }


        private async Task ProcessProductDeleted(ProductDeletedEvent productDeletedEvent)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IInventoryRouteRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var productSnapshot= new ProductSnapshot(
                productDeletedEvent.ProductId,
                productDeletedEvent.InventoryCode,
                productDeletedEvent.Model ?? "No Name",
                productDeletedEvent.Vendor ?? "No Name",
                productDeletedEvent.CategoryName ?? "Unknown",
                productDeletedEvent.IsWorking);

            var route = InventoryRoute.CreateRemoval(
                productSnapshot,
                productDeletedEvent.DepartmentId,
                "Deleted",
                productDeletedEvent.Worker ?? "No Worker",
                "Product removed from system");

            await repository.AddAsync(route);
            await unitOfWork.SaveChangesAsync();
        }


        private async Task ProcessProductUpdated(ProductUpdatedEvent existingProduct)
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IInventoryRouteRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var productClient= scope.ServiceProvider.GetRequiredService<IProductServiceClient>();

            // Get current product details
            var product = await productClient.GetProductByIdAsync(existingProduct.Product.ProductId);
            if (product == null) return;

            var updatedProduct = new ProductSnapshot(
                existingProduct.Product.ProductId,
                existingProduct.Product.InventoryCode,
                product.Model,
                product.Vendor,
                product.CategoryName,
                product.IsWorking);

            // Create an update route entry
            var route = InventoryRoute.CreateUpdate(
                existingProduct.Product,
                updatedProduct,
                product.DepartmentId,
                product.DepartmentName,
                product.Worker,
                $"Product updated: {existingProduct.Changes}");

            await repository.AddAsync(route);
            route.Complete();
            await unitOfWork.SaveChangesAsync();
        }


        public override void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }
}