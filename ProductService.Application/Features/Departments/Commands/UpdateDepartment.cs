using MediatR;
using ProductService.Application.DTOs;
using ProductService.Application.Features.Lookups;
using SharedServices.Exceptions;
using ProductService.Domain.Repositories;

namespace ProductService.Application.Features.Departments.Commands
{
    public class UpdateDepartment
    {
        /// <summary>With <paramref name="ClearBlankFields"/> a blank description or head clears the stored value instead of keeping it.</summary>
        public record Command(int Id, UpdateDepartmentDto DepartmentDto, bool ClearBlankFields = false) : IRequest;

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

                var dto = request.DepartmentDto;
                // A department that already shares its name can still be edited, so only a new name is checked
                if (CatalogNames.IsRenamed(dto.Name, department.Name))
                    await _departmentRepository.EnsureNameIsFreeAsync(dto.Name, department.Id, cancellationToken);

                // A client that leaves a field out keeps its value, so only a caller that sends every field can clear one
                var keepBlank = !request.ClearBlankFields;
                department.Update(
                    dto.Name,
                    keepBlank && string.IsNullOrWhiteSpace(dto.Description) ? department.Description : dto.Description,
                    keepBlank && string.IsNullOrWhiteSpace(dto.DepartmentHead) ? department.DepartmentHead : dto.DepartmentHead,
                    dto.IsActive);

                await _departmentRepository.UpdateAsync(department, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
    }
}