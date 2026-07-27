using FluentValidation;
using MediatR;
using RouteService.Application.DTOs;
using RouteService.Application.Interfaces;
using RouteService.Domain.Exceptions;
using RouteService.Domain.Repositories;

namespace RouteService.Application.Features.Routes.Commands
{
    public class UpdateRoute
    {
        public record Command(int Id, UpdateRouteDto Dto) : IRequest;

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
            private readonly IProductServiceClient _productServiceClient;

            public Handler(
                IInventoryRouteRepository repository,
                IImageService imageService,
                IUnitOfWork unitOfWork,
                IProductServiceClient productServiceClient)
            {
                _repository = repository;
                _imageService = imageService;
                _unitOfWork = unitOfWork;
                _productServiceClient = productServiceClient;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var route = await _repository.GetByIdAsync(request.Id, cancellationToken)
                    ?? throw new RouteException($"Route with ID {request.Id} not found");

                var dto = request.Dto;
                string? oldImageUrl = route.ImageUrl;
                string? newImageUrl = null;

                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                try
                {
                    // Worker/notes. Applied whenever either was supplied - the edit form posts both,
                    // so this also lets a worker or note be cleared.
                    if (dto.ToWorker != null || dto.Notes != null)
                    {
                        route.UpdateExistingRoute(dto.ToWorker, dto.Notes);
                    }

                    // Destination department. The name is looked up here rather than taken from the
                    // caller so the stored id and name cannot disagree.
                    if (dto.ToDepartmentId.HasValue && dto.ToDepartmentId.Value != route.ToDepartmentId)
                    {
                        var department = await _productServiceClient
                            .GetDepartmentByIdAsync(dto.ToDepartmentId.Value, cancellationToken)
                            ?? throw new RouteException($"Department with ID {dto.ToDepartmentId.Value} not found");

                        route.UpdateDestination(department.Id, department.Name);
                    }

                    // Update image if provided
                    if (dto.ImageFile != null && dto.ImageFile.Length > 0)
                    {
                        using var stream = dto.ImageFile.OpenReadStream();
                        newImageUrl = await _imageService.UploadImageAsync(
                            stream,
                            dto.ImageFile.FileName,
                            route.ProductSnapshot.InventoryCode);

                    }
                    route.UpdateImage(newImageUrl);

                    await _repository.UpdateAsync(route, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    await _unitOfWork.CommitTransactionAsync(cancellationToken);

                    // Delete old image after successful update
                    if (!string.IsNullOrEmpty(oldImageUrl) && !string.IsNullOrEmpty(newImageUrl))
                    {
                        await _imageService.DeleteImageAsync(oldImageUrl);
                    }
                }
                catch
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);

                    // Delete new image if update failed
                    if (!string.IsNullOrEmpty(newImageUrl))
                    {
                        await _imageService.DeleteImageAsync(newImageUrl);
                    }

                    throw;
                }
            }
        }
    }
}