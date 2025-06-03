using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Exceptions;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Products.Commands
{
    public record UpdateProductCommand(int Id, UpdateProductDto ProductDto) : IRequest;

    public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand>
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IDepartmentRepository _departmentRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateProductCommandHandler(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IDepartmentRepository departmentRepository,
            IUnitOfWork unitOfWork)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _departmentRepository = departmentRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(UpdateProductCommand request, CancellationToken cancellationToken)
        {
            var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken) ?? 
                throw new NotFoundException($"Product with ID {request.Id} not found");

            var dto = request.ProductDto;

            if (!await _categoryRepository.ExistsByIdAsync(dto.CategoryId, cancellationToken))
                throw new ArgumentException($"Category with ID {dto.CategoryId} not found");

            if (!await _departmentRepository.ExistsByIdAsync(dto.DepartmentId, cancellationToken))
                throw new ArgumentException($"Department with ID {dto.DepartmentId} not found");

            product.Update(dto.Model, dto.Vendor, dto.CategoryId, dto.DepartmentId, dto.ImageUrl, dto.Description);

            await _productRepository.UpdateAsync(product, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}