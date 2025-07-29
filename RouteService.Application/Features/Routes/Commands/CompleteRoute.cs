using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RouteService.Application.Events;
using RouteService.Application.Interfaces;
using RouteService.Domain.Exceptions;
using RouteService.Domain.Repositories;

namespace RouteService.Application.Features.Routes.Commands
{
    public class CompleteRoute
    {
        public record Command(int Id) : IRequest;

        public class Handler : IRequestHandler<Command>
        {
            private readonly IInventoryRouteRepository _repository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IMessagePublisher _messagePublisher;
            private readonly IServiceProvider _serviceProvider;
            private readonly ILogger<Handler> _logger;

            public Handler(
                IInventoryRouteRepository repository, 
                IUnitOfWork unitOfWork, 
                IMessagePublisher messagePublisher,
                IServiceProvider serviceProvider,
                ILogger<Handler> logger)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
                _messagePublisher = messagePublisher;
                _serviceProvider = serviceProvider;
                _logger = logger;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var route = await _repository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new RouteException($"Route with ID {request.Id} not found");

                if (route.IsCompleted)
                    throw new RouteException("Route is already completed");

                route.Complete();

                await _repository.UpdateAsync(route, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // Prepare image data if available
                byte[]? imageData = null;
                string? imageFileName = null;
                
                if(!string.IsNullOrEmpty(route.ImageUrl))
                {
                    try
                    {
                        // Get the image service to read the image
                        using var scope=_serviceProvider.CreateScope();
                        var imageService = scope.ServiceProvider.GetRequiredService<IImageService>();

                        // Extract the file path from the URL
                        var segments=route.ImageUrl.Split('/');
                        if (segments.Length >=2)
                        {
                            var inventoryCode = segments[^2];
                            imageFileName=segments[^1];

                            // Read the image file
                            var imagePath=Path.Combine("wwwroot",route.ImageUrl.TrimStart('/'));
                            if (File.Exists(imagePath))
                            {
                                imageData = await File.ReadAllBytesAsync(imagePath, cancellationToken);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to read image data for route {RouteId}", route.Id);
                    }
                }

                // Now publish the transfer event to update the product
                var transferEvent = new ProductTransferredEvent
                {
                    ProductId = route.ProductSnapshot.ProductId,
                    ToDepartmentId = route.ToDepartmentId,
                    ToWorker = route.ToWorker,
                    ImageData = imageData,
                    ImageFileName = imageFileName,
                    TransferredAt = DateTime.UtcNow
                };

                await _messagePublisher.PublishAsync(transferEvent, "product.transferred", cancellationToken);

                // Publish route completed event for notifications
                var completedEvent = new RouteCompletedEvent
                {
                    RouteId = route.Id,
                    ProductId = route.ProductSnapshot.ProductId,
                    InventoryCode = route.ProductSnapshot.InventoryCode,
                    Model = route.ProductSnapshot.Model,
                    FromDepartmentId = route.FromDepartmentId ?? 0,
                    FromDepartmentName = route.FromDepartmentName ?? "",
                    ToDepartmentId = route.ToDepartmentId,
                    ToDepartmentName = route.ToDepartmentName,
                    CompletedAt = DateTime.UtcNow
                };

                await _messagePublisher.PublishAsync(completedEvent, "route.completed", cancellationToken);
            }
        }
    }
}