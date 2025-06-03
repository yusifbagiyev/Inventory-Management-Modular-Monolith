using AutoMapper;
using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Entities;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Products.Commands
{
    public record CreateProductCommand(CreateProductDto ProductDto) : IRequest<ProductDto>;

    public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, ProductDto>
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IDepartmentRepository _departmentRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public CreateProductCommandHandler(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            IDepartmentRepository departmentRepository,
            IUnitOfWork unitOfWork,
            IMapper mapper)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _departmentRepository = departmentRepository;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        {
            var dto = request.ProductDto;

            if (!await _categoryRepository.ExistsByIdAsync(dto.CategoryId, cancellationToken))
                throw new ArgumentException($"Category with ID {dto.CategoryId} not found");

            if (!await _departmentRepository.ExistsByIdAsync(dto.DepartmentId, cancellationToken))
                throw new ArgumentException($"Department with ID {dto.DepartmentId} not found");

            var product = _mapper.Map<Product>(dto);

            await _productRepository.AddAsync(product, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var createdProduct = await _productRepository.GetByIdAsync(product.Id, cancellationToken);

            return _mapper.Map<ProductDto>(createdProduct);
        }
    }
}