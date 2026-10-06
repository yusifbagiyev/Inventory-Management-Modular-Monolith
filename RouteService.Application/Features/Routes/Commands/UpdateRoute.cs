using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RouteService.Application.DTOs;
using RouteService.Application.Interfaces;
using RouteService.Domain.Entities;
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
                RuleFor(x => x.Dto.ToWorker)
                    .MaximumLength(InventoryRoute.WorkerMaxLength).WithMessage("Worker name cannot exceed 100 characters");
                RuleFor(x => x.Dto.Notes)
                    .MaximumLength(InventoryRoute.NotesMaxLength).WithMessage("Notes cannot exceed 500 characters");
                RuleFor(x => ImageSet.Files(x.Dto.ImageFile, x.Dto.ImageFiles).Count)
                    .LessThanOrEqualTo(ImageSet.MaxImages).WithMessage($"An item can have at most {ImageSet.MaxImages} images");
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
                var destinationBefore = (route.ToDepartmentId, route.ToWorker);

                // A field that was not sent keeps its value, and one sent blank is cleared
                if (dto.ToWorker != null)
                    route.SetToWorker(dto.ToWorker);
                if (dto.Notes != null)
                    route.SetNotes(dto.Notes);

                // Look the name up here so the stored id and name can't disagree
                if (dto.ToDepartmentId.HasValue && dto.ToDepartmentId.Value != route.ToDepartmentId)
                {
                    var department = await _productCatalog.GetDepartmentAsync(dto.ToDepartmentId.Value, cancellationToken)
                        ?? throw new NotFoundException($"Department with ID {dto.ToDepartmentId.Value} not found");
                    if (!department.IsActive)
                        throw new RouteException($"The department {department.Name} is inactive. Choose an active department.");

                    route.UpdateDestination(department.Id, department.Name);
                }

                // An edit must not turn the transfer into one that moves nothing
                if (destinationBefore != (route.ToDepartmentId, route.ToWorker))
                {
                    var product = await _productCatalog.GetProductAsync(route.ProductSnapshot.ProductId, cancellationToken);
                    if (product != null)
                        InventoryRoute.RequireMove(product.DepartmentId, product.Worker, route.ToDepartmentId, route.ToWorker);
                }

                var added = new List<string>();
                foreach (var file in ImageSet.Files(dto.ImageFile, dto.ImageFiles))
                {
                    await using var stream = file.OpenReadStream();
                    var uploaded = await _imageService.UploadImageAsync(stream, file.FileName, route.ProductSnapshot.InventoryCode);
                    _session.OnRollback(() => _imageService.DeleteImageAsync(uploaded));
                    added.Add(uploaded);
                }

                // A single ImageFile from older clients replaces all images
                var (images, removed) = ImageSet.Apply(
                    route.ImageUrls, dto.RemoveImageUrls, added, ImageSet.ResolveCover(dto.CoverImageUrl, added),
                    replaceAll: dto.ImageFile is { Length: > 0 });
                if (!images.SequenceEqual(route.ImageUrls))
                    route.SetImages(images);

                await _repository.UpdateAsync(route, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // Removed images are only deleted once the update is committed
                foreach (var url in removed)
                    _session.AfterCommit((sp, _) => sp.GetRequiredService<ImageStorage>().DeleteAsync(url));
            }
        }
    }
}
