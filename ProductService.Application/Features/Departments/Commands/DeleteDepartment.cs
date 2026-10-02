using MediatR;
using ProductService.Domain.Repositories;
using SharedServices.Exceptions;

namespace ProductService.Application.Features.Departments.Commands
{
    public class DeleteDepartment
    {
        public record Command(int Id) : IRequest;

        public class DeleteDepartmentCommandHandler : IRequestHandler<Command>
        {
            private readonly IDepartmentRepository _departmentRepository;
            private readonly IProductRepository _productRepository;
            private readonly IUnitOfWork _unitOfWork;

            public DeleteDepartmentCommandHandler(
                IDepartmentRepository departmentRepository,
                IProductRepository productRepository,
                IUnitOfWork unitOfWork)
            {
                _departmentRepository = departmentRepository;
                _productRepository = productRepository;
                _unitOfWork = unitOfWork;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var department = await _departmentRepository.GetByIdAsync(request.Id, cancellationToken);
                if (department == null)
                    throw new NotFoundException($"Department with ID {request.Id} not found");

                // The foreign key is Restrict, so the database would refuse anyway.
                // Checking first gives a clear message instead of a 500.
                var productCount = await _productRepository.CountByDepartmentIdAsync(request.Id, cancellationToken);
                if (productCount > 0)
                    throw new ConflictException(
                        $"Cannot delete department '{department.Name}': {productCount} product(s) are still assigned to it. Move them to another department first.");

                // Deleted products are kept and still point at their department.
                var deletedCount = await _productRepository.CountDeletedByDepartmentIdAsync(request.Id, cancellationToken);
                if (deletedCount > 0)
                    throw new ConflictException(
                        $"Cannot delete department '{department.Name}': {deletedCount} deleted product(s) still keep it in their record. Deactivate the department instead.");

                await _departmentRepository.DeleteAsync(department, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}