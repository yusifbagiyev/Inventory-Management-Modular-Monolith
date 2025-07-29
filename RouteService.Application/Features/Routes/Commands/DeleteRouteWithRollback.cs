using MediatR;
using RouteService.Application.Events;
using RouteService.Application.Interfaces;
using RouteService.Domain.Exceptions;
using RouteService.Domain.Repositories;

namespace RouteService.Application.Features.Routes.Commands
{
    public class DeleteRouteWithRollback
    {
        public record Command(int Id): IRequest;

        public class Handler : IRequestHandler<Command>
        {
            private readonly IInventoryRouteRepository _routeRepository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IMessagePublisher _messagePublisher;
            private readonly IImageService _imageService;

            public Handler(IInventoryRouteRepository routeRepository, 
                IUnitOfWork unitOfWork,
                IImageService imageService,
                IMessagePublisher messagePublisher)
            {
                _routeRepository = routeRepository;
                _unitOfWork = unitOfWork;
                _imageService = imageService;
                _messagePublisher = messagePublisher;
            }
            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var routeToDelete=await _routeRepository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new RouteException($"Route with ID {request.Id} not found");

                // Get the previous route for this product
                var previousRoute = await _routeRepository.GetPreviousRouteForProductAsync(
                    routeToDelete.ProductSnapshot.ProductId,
                    routeToDelete.Id,
                    cancellationToken);

                if(previousRoute == null)
                    throw new RouteException("Cannot delete this route. No previous route found to rollback to.");

                // Delete the route's image if it exists
                if(!string.IsNullOrEmpty(routeToDelete.ImageUrl))
                {
                    await _imageService.DeleteImageAsync(routeToDelete.ImageUrl);
                }

                // Create rollback event to update product back to previous route
                var rollbackEvent = new ProductRollbackEvent
                {
                    ProductId = routeToDelete.ProductSnapshot.ProductId,
                    ToDepartmentId = previousRoute.ToDepartmentId,
                    ToWorker = previousRoute.ToWorker,
                    ImageUrl = previousRoute.ImageUrl,
                    RolledBackAt = DateTime.UtcNow,
                    Reason = $"Route {routeToDelete.Id} was deleted"
                };

                // Delete the route
                await _routeRepository.DeleteAsync(routeToDelete, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                //Publish the rollback event
                await _messagePublisher.PublishAsync(rollbackEvent, "product.rollback", cancellationToken);
            }
        }
    }
}