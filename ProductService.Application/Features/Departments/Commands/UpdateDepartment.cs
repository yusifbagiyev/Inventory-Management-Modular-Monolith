using MediatR;
using ProductService.Application.DTOs;
using SharedServices.Exceptions;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Departments.Commands
{
    public class UpdateDepartment
    {
        public record Command(int Id, UpdateDepartmentDto DepartmentDto) : IRequest;

        public class UpdateDepartmentCommandHandler : IRequestHandler<Command>
        {
            private readonly IDepartmentRepository _departmentRepository;
            private readonly IUnitOfWork _unitOfWork;

            public UpdateDepartmentCommandHandler(IDepartmentRepository departmentRepository, IUnitOfWork unitOfWork)
            {
                _departmentRepository = departmentRepository;
                _unitOfWork = unitOfWork;
            }

            public async Task Handle(Command request, CancellationToken cancellationToken)
            {
                var department = await _departmentRepository.GetByIdAsync(request.Id, cancellationToken);
                if (department == null)
                    throw new NotFoundException($"Department with ID {request.Id} not found");

                department.Update(
                    request.DepartmentDto.Name, 
                    request.DepartmentDto.Description,
                    request.DepartmentDto.DepartmentHead,
                    request.DepartmentDto.IsActive);

                await _departmentRepository.UpdateAsync(department, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}