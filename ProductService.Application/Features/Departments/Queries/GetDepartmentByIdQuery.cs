using MediatR;
using ProductService.Application.DTOs;
using ProductService.Domain.Repositories;
using ProductService.Application.Mappings;

namespace ProductService.Application.Features.Departments.Queries
{
    public record GetDepartmentByIdQuery(int Id) : IRequest<DepartmentDto?>;

    public class GetDepartmentByIdQueryHandler : IRequestHandler<GetDepartmentByIdQuery, DepartmentDto?>
    {
        private readonly IDepartmentRepository _departmentRepository;

        public GetDepartmentByIdQueryHandler(IDepartmentRepository departmentRepository)
        {
            _departmentRepository = departmentRepository;
        }

        public async Task<DepartmentDto?> Handle(GetDepartmentByIdQuery request, CancellationToken cancellationToken)
        {
            var department = await _departmentRepository.GetByIdAsync(request.Id, cancellationToken);
            if (department == null) return null;

            return (await new[] { department }.ToDtosAsync(_departmentRepository, cancellationToken))[0];
        }
    }
}
