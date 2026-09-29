using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RouteService.Application.DTOs;
using RouteService.Application.Interfaces;
using RouteService.Domain.Exceptions;
using RouteService.Domain.Repositories;
using SharedServices.Contracts;
using SharedServices.Exceptions;
using SharedServices.Persistence;
using SharedServices.Storage;

namespace RouteService.Application.Features.Routes.Commands
{
    public class UpdateRoute
    {
        public record Command(int Id, UpdateRouteDto Dto) : IRequest, ITransactionalRequest;

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.Id).GreaterThan(0);
                RuleFor(x => x.Dto.Notes)
                    .MaximumLength(500).WithMessage("Notes cannot exceed 500 characters");
            }
        }

        public class Handler : IRequestHandler<Command>
        {
            private readonly IInventoryRouteRepository _repository;
            private readonly IImageService _imageService;
            private readonly IUnitOfWork _unitOfWork;
            private readonly IProductCatalog _productCatalog;
            private readonly DbSession _session;

            public Handler(
                IInventoryRouteRepository repository,
                IImageService imageService,
                IUnitOfWork unitOfWork,
                IProductCatalog productCatalog,
                DbSession session)
            {
                _repository = repository;
                _imageService = imageService;
                _unitOfWork = unitOfWork;
                _productCatalog = productCatalog;
                _session = session;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var route = await _repository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new NotFoundException($"Route with ID {request.Id} not found");

                if (route.IsCompleted)
                    throw new RouteException("Cannot update a completed route");

                var dto = request.Dto;
                var oldImageUrl = route.ImageUrl;

                // Worker/notes. Applied whenever either was supplied - the edit form posts both,
                // so this also lets a worker or note be cleared.
                if (dto.ToWorker != null || dto.Notes != null)
                    route.UpdateExistingRoute(dto.ToWorker, dto.Notes);

                // Destination department. The name is looked up here rather than taken from the
                // caller so the stored id and name cannot disagree.
                if (dto.ToDepartmentId.HasValue && dto.ToDepartmentId.Value != route.ToDepartmentId)
                {
                    var department = await _productCatalog.GetDepartmentAsync(dto.ToDepartmentId.Value, cancellationToken)
                        ?? throw new NotFoundException($"Department with ID {dto.ToDepartmentId.Value} not found");

                    route.UpdateDestination(department.Id, department.Name);
                }

                string? newImageUrl = null;
                if (dto.ImageFile != null && dto.ImageFile.Length > 0)
                {
                    await using var stream = dto.ImageFile.OpenReadStream();
                    newImageUrl = await _imageService.UploadImageAsync(
                        stream,
                        dto.ImageFile.FileName,
                        route.ProductSnapshot.InventoryCode);
                    var uploaded = newImageUrl;
                    _session.OnRollback(() => _imageService.DeleteImageAsync(uploaded));
                    route.UpdateImage(newImageUrl);
                }

                await _repository.UpdateAsync(route, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                if (newImageUrl != null && !string.IsNullOrEmpty(oldImageUrl))
                    _session.AfterCommit((sp, _) => sp.GetRequiredService<ImageStorage>().DeleteAsync(oldImageUrl));
            }
        }
    }
}
