using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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
    /// <summary>
    /// Raised when a message can never succeed no matter how often it is retried
    /// (bad payload, or it references data that no longer exists). Such messages go
    /// straight to the dead-letter queue instead of being requeued.
    /// </summary>
    public class PermanentMessageException : Exception
    {
        public PermanentMessageException(string message) : base(message) { }
    }

    public class RabbitMQConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RabbitMQConsumer> _logger;
        private IConnection? _connection;
        private IModel? _channel;
        private readonly string _queueName = "product-transfers";
        private readonly string _deadLetterQueueName = "product-transfers-dead";
        private readonly IConfiguration _configuration;

        public RabbitMQConsumer(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<RabbitMQConsumer> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;

            InitializeRabbitMQ();
        }

        private void InitializeRabbitMQ()
        {
            try
            {
                var hostname = _configuration["RabbitMQ:HostName"] ??
                                              Environment.GetEnvironmentVariable("RabbitMQ__HostName") ??
                                              "localhost";

                var username = _configuration["RabbitMQ:UserName"] ??
                              Environment.GetEnvironmentVariable("RabbitMQ__UserName") ??
                              "guest";

                var password = _configuration["RabbitMQ:Password"] ??
                              Environment.GetEnvironmentVariable("RabbitMQ__Password") ??
                              "guest";

                var port = int.Parse(_configuration["RabbitMQ:Port"] ??
                                    Environment.GetEnvironmentVariable("RabbitMQ__Port") ??
                                    "5672");

                _logger.LogInformation($"Connecting RabbitMQ Consumer to {hostname}:{port} with user {username}");

                var factory = new ConnectionFactory
                {
                    HostName = hostname,
                    UserName = username,
                    Password = password,
                    Port = port,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                    // IModel is not thread-safe; let the client dispatch async handlers on its own
                    // consumer thread instead of acking from an arbitrary continuation thread.
                    DispatchConsumersAsync = true
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                _channel.ExchangeDeclare("inventory-events", ExchangeType.Topic, durable: true);
                _channel.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false);
                _channel.QueueBind(_queueName, "inventory-events", "product.transferred");

                // Parking queue for messages that can never succeed. Declared separately so the
                // existing live queue keeps its original arguments (redeclaring it with new
                // arguments would fail with PRECONDITION_FAILED).
                _channel.QueueDeclare(_deadLetterQueueName, durable: true, exclusive: false, autoDelete: false);

                // Bound prefetch: without this the broker floods the consumer with unacked messages.
                _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

                _logger.LogInformation("RabbitMQ Consumer successfully connected");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize RabbitMQ connection");
                throw;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (sender, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);

                    ProductTransferredEvent? transferEvent;
                    try
                    {
                        transferEvent = JsonSerializer.Deserialize<ProductTransferredEvent>(message);
                    }
                    catch (JsonException ex)
                    {
                        throw new PermanentMessageException($"Message is not a valid ProductTransferredEvent: {ex.Message}");
                    }

                    if (transferEvent != null)
                        await ProcessProductTransfer(transferEvent);

                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (PermanentMessageException ex)
                {
                    // Retrying can never help - park it and move on. Logged without a stack trace:
                    // an endlessly redelivered message used to write one full trace per redelivery.
                    _logger.LogWarning("Dead-lettering unprocessable product transfer: {Reason}", ex.Message);
                    DeadLetter(ea);
                }
                catch (DbUpdateException ex)
                {
                    // Constraint violations (e.g. the target department no longer exists) are
                    // permanent for this payload - never requeue them.
                    _logger.LogWarning("Dead-lettering product transfer rejected by the database: {Reason}",
                        ex.InnerException?.Message ?? ex.Message);
                    DeadLetter(ea);
                }
                catch (Exception ex)
                {
                    // Possibly transient (broker/DB blip): allow exactly one retry, then park it.
                    if (ea.Redelivered)
                    {
                        _logger.LogError(ex, "Product transfer failed again after redelivery - dead-lettering");
                        DeadLetter(ea);
                    }
                    else
                    {
                        _logger.LogWarning("Product transfer failed, requeueing once: {Reason}", ex.Message);
                        _channel.BasicNack(ea.DeliveryTag, false, requeue: true);
                    }
                }
            };
            _channel.BasicConsume(_queueName, false, consumer);
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        /// <summary>
        /// Moves the delivery to the parking queue and acks the original, so it can be inspected
        /// or replayed later without blocking the live queue.
        /// </summary>
        private void DeadLetter(BasicDeliverEventArgs ea)
        {
            try
            {
                var properties = _channel!.CreateBasicProperties();
                properties.Persistent = true;
                _channel.BasicPublish(exchange: "", routingKey: _deadLetterQueueName,
                    basicProperties: properties, body: ea.Body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to move a message to {DeadLetterQueue}", _deadLetterQueueName);
            }
            finally
            {
                _channel!.BasicAck(ea.DeliveryTag, false);
            }
        }

        private async Task ProcessProductTransfer(ProductTransferredEvent transferEvent)
        {
            using var scope = _serviceProvider.CreateScope();
            var productRepository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
            var departmentRepository = scope.ServiceProvider.GetRequiredService<IDepartmentRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var imageService = scope.ServiceProvider.GetRequiredService<IImageService>();

            var product = await productRepository.GetByIdAsync(transferEvent.ProductId);
            if (product == null)
            {
                _logger.LogWarning($"Product {transferEvent.ProductId} not found");
                return;
            }

            // The event carries a department id owned by RouteService's database; the department
            // may have been deleted here since the route was completed. Writing it anyway would
            // violate the foreign key on every retry, so treat it as permanent up front.
            if (!await departmentRepository.ExistsByIdAsync(transferEvent.ToDepartmentId))
                throw new PermanentMessageException(
                    $"Target department {transferEvent.ToDepartmentId} no longer exists (product {transferEvent.ProductId})");

            // Update product info
            product.UpdateAfterRouting(transferEvent.ToDepartmentId, transferEvent.ToWorker);

            // Update image if provided
            if (transferEvent.ImageData != null && transferEvent.ImageData.Length > 0)
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
