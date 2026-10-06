using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RouteService.Domain.Exceptions;
using RouteService.Domain.Repositories;
using SharedServices.Exceptions;
using SharedServices.Persistence;
using SharedServices.Storage;

namespace RouteService.Application.Features.Routes.Commands
{
    /// <summary>Removes a completed route record for good, for a test or mistaken transfer; the product stays where it is now.</summary>
    public class PurgeRoute
    {
        public record Command(int Id) : IRequest, ITransactionalRequest;

        public class Handler : IRequestHandler<Command>
        {
            private readonly IInventoryRouteRepository _repository;
            private readonly IUnitOfWork _unitOfWork;
            private readonly DbSession _session;

            public Handler(IInventoryRouteRepository repository, IUnitOfWork unitOfWork, DbSession session)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
                _session = session;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var route = await _repository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new NotFoundException($"Route with ID {request.Id} not found");

                // A pending transfer is withdrawn with the ordinary delete, which may need approval
                if (!route.IsCompleted)
                    throw new RouteException("Only a completed route can be deleted permanently. Delete a pending transfer instead.");

                await _repository.DeleteAsync(route, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // The history's own photo copies go only once the removal has committed
                foreach (var imageUrl in route.ImageUrls)
                    _session.AfterCommit((sp, _) => sp.GetRequiredService<ImageStorage>().DeleteAsync(imageUrl));
            }
        }
    }
}
