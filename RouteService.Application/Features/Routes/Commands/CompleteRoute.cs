using MediatR;
using RouteService.Domain.Enums;
using RouteService.Domain.Exceptions;
using RouteService.Domain.Repositories;
using SharedServices.Contracts;
using SharedServices.Events;
using SharedServices.Exceptions;
using SharedServices.Persistence;

namespace RouteService.Application.Features.Routes.Commands
{
    public class CompleteRoute
    {
        public record Command(int Id) : IRequest, ITransactionalRequest;

        public class Handler : IRequestHandler<Command>
        {
            private readonly IInventoryRouteRepository _repository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IProductTransfers _productTransfers;
            private readonly IPublisher _publisher;

            public Handler(
                IInventoryRouteRepository repository,
                IUnitOfWork unitOfWork,
                IProductTransfers productTransfers,
                IPublisher publisher)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
                _productTransfers = productTransfers;
                _publisher = publisher;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var route = await _repository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new NotFoundException($"Route with ID {request.Id} not found");

                if (route.IsCompleted)
                    throw new RouteException("Route is already completed");

                route.Complete();
                await _repository.UpdateAsync(route, cancellationToken);
                // The row version makes a concurrent second completion fail here instead of
                // moving the product twice.
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // Same transaction: the route is never marked complete without the product moving.
                if (route.RouteType == RouteType.Transfer)
                {
                    await _productTransfers.ApplyTransferAsync(
                        route.ProductSnapshot.ProductId,
                        route.ToDepartmentId,
                        route.ToWorker,
                        route.ImageUrls,
                        cancellationToken);
                }

                await _publisher.Publish(new RouteCompletedEvent
                {
                    RouteId = route.Id,
                    ProductId = route.ProductSnapshot.ProductId,
                    InventoryCode = route.ProductSnapshot.InventoryCode,
                    Model = route.ProductSnapshot.Model,
                    Vendor = route.ProductSnapshot.Vendor,
                    CategoryName = route.ProductSnapshot.CategoryName,
                    FromDepartmentName = route.FromDepartmentName ?? string.Empty,
                    FromWorker = route.FromWorker,
                    ToDepartmentId = route.ToDepartmentId,
                    ToDepartmentName = route.ToDepartmentName,
                    ToWorker = route.ToWorker,
                    Notes = route.Notes,
                    ImageUrl = route.ImageUrl,
                    CompletedAt = route.CompletedAt
                }, cancellationToken);
            }
        }
    }
}
