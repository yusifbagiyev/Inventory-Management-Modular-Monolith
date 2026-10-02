using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RouteService.Domain.Exceptions;
using RouteService.Domain.Repositories;
using SharedServices.Exceptions;
using SharedServices.Persistence;
using SharedServices.Storage;

namespace RouteService.Application.Features.Routes.Commands
{
    public class DeleteRoute
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

                if (route.IsCompleted)
                    throw new RouteException("Cannot delete completed route. Completed routes are part of the audit trail.");

                await _repository.DeleteAsync(route, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // The files go only once the delete is committed
                foreach (var imageUrl in route.ImageUrls)
                    _session.AfterCommit((sp, _) => sp.GetRequiredService<ImageStorage>().DeleteAsync(imageUrl));
            }
        }
    }
}
