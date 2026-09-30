using FluentValidation;
using MediatR;
using RouteService.Application.DTOs;
using RouteService.Application.Interfaces;
using RouteService.Domain.Entities;
using RouteService.Domain.Exceptions;
using RouteService.Domain.Repositories;
using RouteService.Domain.ValueObjects;
using SharedServices.Contracts;
using SharedServices.Exceptions;
using SharedServices.Persistence;
using SharedServices.Storage;
using RouteService.Application.Mappings;

namespace RouteService.Application.Features.Routes.Commands
{
    public class TransferInventory
    {
        public record Command(TransferInventoryDto Dto) : IRequest<InventoryRouteDto>, ITransactionalRequest;

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.Dto.ProductId).GreaterThan(0);
                RuleFor(x => x.Dto.ToDepartmentId).GreaterThan(0);
                RuleFor(x => x.Dto.Notes).MaximumLength(500);
                RuleFor(x => ImageSet.Files(x.Dto.ImageFile, x.Dto.ImageFiles).Count)
                    .LessThanOrEqualTo(ImageSet.MaxImages).WithMessage($"An item can have at most {ImageSet.MaxImages} images");
            }
        }

        public class Handler : IRequestHandler<Command, InventoryRouteDto>
        {
            private readonly IInventoryRouteRepository _repository;
            private readonly IProductCatalog _productCatalog;
            private readonly IImageService _imageService;
            private readonly IUnitOfWork _unitOfWork;
            private readonly DbSession _session;

            public Handler(
                IInventoryRouteRepository repository,
                IProductCatalog productCatalog,
                IImageService imageService,
                IUnitOfWork unitOfWork,
                DbSession session)
            {
                _repository = repository;
                _productCatalog = productCatalog;
                _imageService = imageService;
                _unitOfWork = unitOfWork;
                _session = session;
            }

            public async Task<InventoryRouteDto> Handle(Command request, CancellationToken cancellationToken)
            {
                var dto = request.Dto;

                var product = await _productCatalog.GetProductAsync(dto.ProductId, cancellationToken)
                    ?? throw new NotFoundException($"Product {dto.ProductId} not found");

                var toDepartment = await _productCatalog.GetDepartmentAsync(dto.ToDepartmentId, cancellationToken)
                    ?? throw new NotFoundException($"Department {dto.ToDepartmentId} not found");
                if (!toDepartment.IsActive)
                    throw new RouteException($"The department {toDepartment.Name} is inactive. Choose an active department.");

                // Two open transfers for one product could be completed in either order, leaving the
                // product wherever the last one pointed.
                if (await _repository.HasPendingRouteForProductAsync(product.Id, cancellationToken))
                    throw new RouteException("This product already has a pending transfer. Complete or delete it first.");

                var imageUrls = new List<string>();
                foreach (var file in ImageSet.Files(dto.ImageFile, dto.ImageFiles))
                {
                    await using var stream = file.OpenReadStream();
                    var uploaded = await _imageService.UploadImageAsync(stream, file.FileName, product.InventoryCode);
                    _session.OnRollback(() => _imageService.DeleteImageAsync(uploaded));
                    imageUrls.Add(uploaded);
                }

                var route = InventoryRoute.CreateTransfer(
                    new ProductSnapshot(
                        product.Id,
                        product.InventoryCode,
                        product.Model,
                        product.Vendor,
                        product.CategoryName,
                        product.IsWorking),
                    product.DepartmentId,
                    product.DepartmentName,
                    toDepartment.Id,
                    toDepartment.Name,
                    product.Worker,
                    dto.ToWorker,
                    imageUrls,
                    dto.Notes);

                await _repository.AddAsync(route, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return route.ToDto();
            }
        }
    }
}
