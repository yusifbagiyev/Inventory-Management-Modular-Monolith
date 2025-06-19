using MediatR;
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

            public Handler(IInventoryRouteRepository repository, IUnitOfWork unitOfWork)
            {
                _repository = repository;
                _unitOfWork = unitOfWork;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var route = await _repository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new RouteException($"Route with ID {request.Id} not found");

                route.Complete();

                await _repository.UpdateAsync(route, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
