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
            private readonly IUnitOfWork _unitOfWork;

            public DeleteDepartmentCommandHandler(IDepartmentRepository departmentRepository, IUnitOfWork unitOfWork)
            {
                _departmentRepository = departmentRepository;
                _unitOfWork = unitOfWork;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var department = await _departmentRepository.GetByIdAsync(request.Id, cancellationToken);
                if (department == null)
                    throw new NotFoundException($"Department with ID {request.Id} not found");

                await _departmentRepository.DeleteAsync(department, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}